using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using NutDesk.Common;
using NutDesk.Common.Updater;
using NutDesk.Updating;
using OldParams = NutDesk.Common.OldParams;
using Settings = NutDesk.Properties.Settings;

namespace NutDesk
{
    /// <summary>
    /// Application entry point. This replaces the VB.NET application framework
    /// (<c>Microsoft.VisualBasic.ApplicationServices.WindowsFormsApplicationBase</c> and the generated
    /// <c>MyApplication</c> class) the original project relied on, reproducing the four behaviours that
    /// were configured in Application.myapp: visual styles, single instance, shutdown after the main form
    /// closes, and saving user settings on exit.
    /// </summary>
    internal static class Program
    {
        /// <summary>Default culture for output so logs can be shared with the project.</summary>
        internal static readonly CultureInfo DEF_CULTURE_INFO = CultureInfo.InvariantCulture;

        // Single-instance plumbing. The VB framework used a per-user, per-session mutex derived from the
        // assembly GUID and re-activated the running instance instead of starting a second one; the two
        // named objects below do the same thing explicitly. "Local\" scopes them to the logon session,
        // which is what SingleInstance=true meant: two different users may each run their own copy.
        private const string SingleInstanceMutexName = @"Local\NutDesk.SingleInstance";
        private const string ActivateSignalName = @"Local\NutDesk.ActivateRunningInstance";

        // How long after startup the Updates folder is tidied. Right after an update, the copy that installed
        // this one may still be running from the staging folder that is about to be removed.
        private static readonly TimeSpan UpdatesCleanupDelay = TimeSpan.FromSeconds(30);

        private static Mutex singleInstanceMutex;
        private static EventWaitHandle activateSignal;
        private static RegisteredWaitHandle activateRegistration;

        /// <summary>
        /// The version this start was updated from, when the in-place updater started it with --updated-from;
        /// null otherwise. Already logged by <see cref="Startup"/>; MainForm adds the toast.
        /// </summary>
        internal static string UpdatedFromVersion { get; private set; }

        /// <summary>
        /// Why an in-place update failed, when the updater restored this version and started it with
        /// --update-failed; null otherwise. Already logged by <see cref="Startup"/>; MainForm shows the message.
        /// </summary>
        internal static string UpdateFailedReason { get; private set; }

        [STAThread]
        private static int Main(string[] args)
        {
            // The apply mode of an in-place update comes before anything else - the single-instance mutex, the
            // settings, the notification service, the main window: this copy is the new version, started from the
            // staging folder by the old one to copy itself over it. The command line is a contract between
            // versions; see UpdateCommandLine.
            if (UpdateCommandLine.IsApplyMode(args))
            {
                return ApplyUpdateMode.Run(args);
            }

            bool isFirstInstance;
            singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out isFirstInstance);

            if (!isFirstInstance)
            {
                // Another copy owns the mutex: poke it so it brings its window forward, then leave.
                // This mirrors StartupNextInstance, whose default behaviour was to activate MainForm.
                SignalRunningInstance();
                return 0;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // The VB framework routed exceptions on the UI thread into its UnhandledException event
                // rather than letting the default WinForms dialog appear; CatchException restores that.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

                Startup(args);

                var mainForm = new MainForm();
                MainForm.Instance = mainForm;

                StartListeningForActivation(mainForm);

                // ShutdownMode 0 (AfterMainFormCloses) is exactly what Application.Run gives us.
                Application.Run(mainForm);
            }
            finally
            {
                // SaveMySettingsOnExit=true.
                try
                {
                    Settings.Default.Save();
                }
                catch (Exception ex)
                {
                    AppGlobals.LogFile.LogTracing("Unable to save settings on exit.", LogLvl.LOG_ERROR, null);
                    AppGlobals.LogFile.LogException(ex, null);
                }

                StopListeningForActivation();

                if (singleInstanceMutex != null)
                {
                    try
                    {
                        singleInstanceMutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // Not the owner any more (the process is tearing down); nothing to release.
                    }

                    singleInstanceMutex.Dispose();
                    singleInstanceMutex = null;
                }
            }

            return 0;
        }

        #region Startup

        private static void Startup(IList<string> args)
        {
            AppGlobals.LogFile.LogTracing(
                string.Format("{0} v{1} starting up.", AppGlobals.ProgramName, AppGlobals.ProgramVersion),
                LogLvl.LOG_NOTICE, null);
            AppGlobals.LogFile.LogTracing("Data storage path: " + AppGlobals.DataDirectory, LogLvl.LOG_NOTICE, null);

            // Before MainForm creates the notification service, which opens the outbox this may move.
            AppGlobals.MigrateLegacyData();

            Settings.Default.SettingsLoaded += OnSettingsFirstLoaded;
            Settings.Default.PropertyChanged += OnPropertyChanged;

            AppGlobals.LogFile.LogTracing("Event handlers configured.", LogLvl.LOG_DEBUG, null);

            ApplyLoggingSettings();

            // Starting without previous settings: a first run, a new version, or this copy moved to another folder.
            if (Settings.Default.IsFirstRun)
            {
                try
                {
                    // Carry over the settings of the previous version run from this same folder.
                    Settings.Default.Upgrade();
                }
                catch (ConfigurationErrorsException ex)
                {
                    AppGlobals.LogFile.LogTracing("Error encountered while trying to upgrade Settings:", LogLvl.LOG_ERROR, null);
                    AppGlobals.LogFile.LogException(ex, null);
                }

                if (!Settings.Default.IsFirstRun)
                {
                    AppGlobals.LogFile.LogTracing("Settings carried over from the previous version run from this folder.",
                                                  LogLvl.LOG_NOTICE, null);
                }
                else
                {
                    AppGlobals.LogFile.LogTracing("No previous settings for this location (" + Application.ExecutablePath + ").",
                                                  LogLvl.LOG_NOTICE, null);

                    // .NET keys the settings on the executable's path, so a copy extracted into another folder, a
                    // renamed folder or a new drive letter starts empty. The settings are still there, under
                    // another path's folder.
                    ImportSettingsFromAnotherLocation();
                }

                // If Settings still appear new, check if old Registry preferences are leftover.
                if (Settings.Default.IsFirstRun && OldParams.NutDesk_Params.ParamsExist)
                {
                    AppGlobals.LogFile.LogTracing("Previous preferences data detected in the Registry.", LogLvl.LOG_NOTICE, null,
                        Properties.Resources.DetectedPreviousPrefsData);

                    Forms.UpgradePrefsDialog.Instance.ShowDialog();
                }

                Settings.Default.IsFirstRun = false;
                Settings.Default.Save();
            }

            ReadUpdateOutcome(args);

            // A copy moved to another folder, or updated from one, must be the copy Windows starts at logon.
            AutoStart.Reconcile(Settings.Default.StartWithWindows);

            ScheduleUpdatesCleanup();

            AppGlobals.LogFile.LogTracing("Startup complete.", LogLvl.LOG_DEBUG, null);
        }

        /// <summary>
        /// Adopts the settings this user last saved from another location of NutDesk.exe: the most recently written
        /// %LOCALAPPDATA%\NutDesk\*.exe_Url_*\&lt;version&gt;\user.config other than this copy's own, copied to this
        /// copy's path and reloaded. The protected values (NUT and SMTP passwords) carry over, as they are bound to
        /// the Windows user, not to the path. A file that cannot be read is skipped for the next newest one.
        /// </summary>
        /// <returns>True when settings were imported.</returns>
        private static bool ImportSettingsFromAnotherLocation()
        {
            string currentPath;
            try
            {
                currentPath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            }
            catch (ConfigurationErrorsException ex)
            {
                AppGlobals.LogFile.LogTracing("Unable to locate this copy's settings file.", LogLvl.LOG_WARNING, null);
                AppGlobals.LogFile.LogException(ex, null);
                return false;
            }

            List<FileInfo> candidates;
            try
            {
                // <root>\<exe name>_Url_<hash>\<version>\user.config: two folders up from this copy's version folder.
                var currentFolder = Path.GetDirectoryName(currentPath);
                var root = Path.GetDirectoryName(Path.GetDirectoryName(currentFolder));
                if (root == null || !Directory.Exists(root))
                {
                    return false;
                }

                candidates = Directory.GetDirectories(root, "*.exe_Url_*")
                                      .SelectMany(folder => Directory.GetDirectories(folder))
                                      .Where(folder => !string.Equals(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar),
                                                                      Path.GetFullPath(currentFolder).TrimEnd(Path.DirectorySeparatorChar),
                                                                      StringComparison.OrdinalIgnoreCase))
                                      .Select(folder => new FileInfo(Path.Combine(folder, "user.config")))
                                      .Where(file => file.Exists)
                                      .OrderByDescending(file => file.LastWriteTimeUtc)
                                      .ToList();
            }
            catch (Exception ex)
            {
                AppGlobals.LogFile.LogTracing("Unable to look for the settings of another NutDesk location.", LogLvl.LOG_WARNING, null);
                AppGlobals.LogFile.LogException(ex, null);
                return false;
            }

            foreach (var candidate in candidates)
            {
                if (!IsNutDeskSettingsFile(candidate.FullName))
                {
                    AppGlobals.LogFile.LogTracing("Ignoring unreadable settings file " + candidate.FullName + ".", LogLvl.LOG_WARNING, null);
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(currentPath));
                    File.Copy(candidate.FullName, currentPath, true);
                    Settings.Default.Reload();

                    // Reading a value makes the provider parse the file now, inside this try, rather than at some
                    // later access that expects the settings to work.
                    var stillFirstRun = Settings.Default.IsFirstRun;

                    AppGlobals.LogFile.LogTracing("Settings imported from " + candidate.FullName + " (IsFirstRun=" + stillFirstRun + ").",
                                                  LogLvl.LOG_NOTICE, null, Properties.Resources.Log_SettingsImported);
                    return true;
                }
                catch (Exception ex)
                {
                    AppGlobals.LogFile.LogTracing("Unable to import the settings file " + candidate.FullName + ".", LogLvl.LOG_WARNING, null);
                    AppGlobals.LogFile.LogException(ex, null);

                    // Back to a clean slate, so the next candidate - or the defaults - start from nothing.
                    try
                    {
                        File.Delete(currentPath);
                        Settings.Default.Reload();
                    }
                    catch (Exception cleanupEx)
                    {
                        AppGlobals.LogFile.LogTracing("Unable to discard the failed import: " + cleanupEx.Message, LogLvl.LOG_WARNING, null);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// True when the file is well-formed XML with this application's user settings section in it.
        /// </summary>
        private static bool IsNutDeskSettingsFile(string path)
        {
            try
            {
                var document = XDocument.Load(path);
                return document.Root?.Element("userSettings")?.Element(typeof(Settings).FullName) != null;
            }
            catch (Exception ex) when (ex is XmlException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reads --updated-from and --update-failed, which the in-place updater starts NutDesk with (see
        /// UpdateCommandLine), and logs them in the event log. MainForm adds the toast and the message.
        /// </summary>
        private static void ReadUpdateOutcome(IList<string> args)
        {
            var from = UpdateCommandLine.GetValue(args, UpdateCommandLine.UpdatedFrom);
            if (!string.IsNullOrWhiteSpace(from))
            {
                // Shown to the user, so only ever a version: anything else on the command line is cut down to size.
                UpdatedFromVersion = UpdateUtil.TryParseReleaseVersion(from, out Version version)
                    ? UpdateUtil.FormatVersion(version)
                    : new string(from.Trim().Take(32).ToArray());

                AppGlobals.LogFile.LogTracing($"Updated from {UpdatedFromVersion} to {UpdateUtil.RunningVersionText}.", LogLvl.LOG_NOTICE, null,
                                              UpdateText.Format(Properties.Resources.Update_UpdatedFrom, UpdatedFromVersion,
                                                                UpdateUtil.RunningVersionText));
            }

            var failure = UpdateCommandLine.GetValue(args, UpdateCommandLine.UpdateFailed);
            if (failure != null)
            {
                UpdateFailedReason = string.IsNullOrWhiteSpace(failure)
                    ? UpdateText.Describe(UpdateFailure.Unexpected, null)
                    : failure.Trim();

                AppGlobals.LogFile.LogTracing("In-place update failed; this version was restored. Reason: " + UpdateFailedReason,
                                              LogLvl.LOG_ERROR, null,
                                              UpdateText.Format(Properties.Resources.Update_InstallFailed, UpdateFailedReason));
            }
        }

        /// <summary>
        /// Removes, in the background and a little later, what finished or abandoned updates left in the Updates
        /// folder. Whatever is still locked is tried again at the next start.
        /// </summary>
        private static void ScheduleUpdatesCleanup()
        {
            Task.Run(async () =>
            {
                await Task.Delay(UpdatesCleanupDelay).ConfigureAwait(false);
                UpdateInstaller.CleanupUpdatesDirectory(UpdateUtil.RunningVersion);
            });
        }

        /// <summary>
        /// Handles validation of Settings when loaded.
        /// </summary>
        private static void OnSettingsFirstLoaded(object sender, SettingsLoadedEventArgs e)
        {
            AppGlobals.LogFile.LogTracing("OnSettingsFirstLoaded event raised.", LogLvl.LOG_DEBUG, null);

            // Verify that encrypted data can be decrypted
            try
            {
                Settings.Default.NUT_Username?.ToString();
            }
            catch (Exception ex)
            {
                AppGlobals.LogFile.LogTracing("Error attempting to decrypt encrypted data. Resetting to defaults.",
                    LogLvl.LOG_ERROR, null, Properties.Resources.Log_Str_ErrorDecrypting);
                AppGlobals.LogFile.LogException(ex, null);

                Settings.Default.NUT_Username = new SerializedProtectedString();
                Settings.Default.NUT_Password = new SerializedProtectedString();
            }

            if (!(Settings.Default.NUT_PollIntervalMsec > 0))
            {
                AppGlobals.LogFile.LogTracing("Incorrect value of " + Settings.Default.NUT_PollIntervalMsec +
                    " for Poll Delay/Interval, resetting to default.", LogLvl.LOG_ERROR, null);

                // The VB original assigned My.Settings back to MySettings.Default here, but both resolve to
                // the same object: the "reset" was a self-assignment and left the bad value in place. Read
                // the declared default off the SettingsProperty instead, so the recovery actually recovers.
                Settings.Default.NUT_PollIntervalMsec = Convert.ToInt32(
                    Settings.Default.Properties["NUT_PollIntervalMsec"].DefaultValue, DEF_CULTURE_INFO);
            }
        }

        /// <summary>
        /// Raised when any Settings property is changed through a set accessor, or when reloaded/reset.
        /// </summary>
        private static void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            AppGlobals.LogFile.LogTracing("Handling OnPropertyChanged for " + e.PropertyName, LogLvl.LOG_DEBUG, null);
            if (e.PropertyName == "LG_LogToFile" || e.PropertyName == "LG_LogLevel")
            {
                AppGlobals.LogFile.LogTracing("Settings property changed for logging subsystem, updating...", LogLvl.LOG_DEBUG, null);
                ApplyLoggingSettings();
            }
        }

        private static void ApplyLoggingSettings()
        {
            AppGlobals.LogFile.IsWritingToFile = Settings.Default.LG_LogToFile;
            AppGlobals.LogFile.LogLevelValue = (LogLvl)Settings.Default.LG_LogLevel;
        }

        #endregion

        #region Unhandled exceptions

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            AppGlobals.LogFile.LogTracing("UnhandledException", LogLvl.LOG_ERROR, null);
            CrashReporter.Report(e.Exception, terminating: false);
        }

        private static void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            AppGlobals.LogFile.LogTracing("AppDomainUnhandledException", LogLvl.LOG_ERROR, null);

            // The CLR tears the process down as soon as this returns, so unlike the UI-thread path the
            // dialog has to be modal or the user would never get to read it.
            var ex = e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject));
            CrashReporter.Report(ex, terminating: true);
        }

        #endregion

        #region Single instance

        private static void SignalRunningInstance()
        {
            try
            {
                EventWaitHandle signal;
                if (EventWaitHandle.TryOpenExisting(ActivateSignalName, out signal))
                {
                    using (signal)
                    {
                        signal.Set();
                    }
                }
            }
            catch (Exception)
            {
                // The running instance may be shutting down. Nothing useful to do but exit quietly.
            }
        }

        private static void StartListeningForActivation(Form mainForm)
        {
            activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateSignalName);
            activateRegistration = ThreadPool.RegisterWaitForSingleObject(
                activateSignal,
                (state, timedOut) => ActivateMainForm(mainForm),
                null,
                Timeout.Infinite,
                false);
        }

        private static void ActivateMainForm(Form mainForm)
        {
            try
            {
                if (mainForm == null || mainForm.IsDisposed || !mainForm.IsHandleCreated)
                {
                    return;
                }

                mainForm.BeginInvoke(new Action(() =>
                {
                    if (!mainForm.Visible)
                    {
                        mainForm.Show();
                    }

                    if (mainForm.WindowState == FormWindowState.Minimized)
                    {
                        mainForm.WindowState = FormWindowState.Normal;
                    }

                    mainForm.Activate();
                }));
            }
            catch (Exception ex)
            {
                AppGlobals.LogFile.LogTracing("Unable to activate the running instance.", LogLvl.LOG_ERROR, null);
                AppGlobals.LogFile.LogException(ex, null);
            }
        }

        private static void StopListeningForActivation()
        {
            if (activateRegistration != null)
            {
                activateRegistration.Unregister(null);
                activateRegistration = null;
            }

            if (activateSignal != null)
            {
                activateSignal.Dispose();
                activateSignal = null;
            }
        }

        #endregion
    }
}
