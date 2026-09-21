using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NutDesk.Common
{
    /// <summary>
    /// The VB "Module AppGlobals". Its members were globally visible there; here they are static members of
    /// this class, so callers write AppGlobals.LogFile and so on.
    /// </summary>
    public static class AppGlobals
    {
        #region Constants/Shareds

        /// <summary>
        /// The command-line switch that keeps the data (logs, email outbox, crash reports) next to the executable.
        /// </summary>
        public const string PARAM_PERSIST_DATA_IN_STARTUP_PATH = "-PersistDataInStartupPath";

        // The folder under %LOCALAPPDATA% that holds everything NutDesk writes there: the data files, the Updates
        // folder and, in NutDesk.exe_Url_* subfolders, the .NET user settings. It is named after the product and
        // carries no version, so a new version finds the logs and the email outbox of the one before it.
        private const string LOCAL_DATA_FOLDER_NAME = "NutDesk";

        // Where the data went up to the first public release: Application.LocalUserAppDataPath, that is
        // %LOCALAPPDATA%\<company>\<product>\<version>, with both names "NutDesk". Only read by the migration.
        private const string LEGACY_PRODUCT_FOLDER_NAME = "NutDesk";

        private const string UPDATES_FOLDER_NAME = "Updates";
        private const string NOTIFICATIONS_FOLDER_NAME = "Notifications";
        private const string OUTBOX_FILE_NAME = "outbox.json";

        public static readonly string ProgramName =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product;
        public static readonly string ProgramVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public static readonly string ShortProgramVersion =
            ProgramVersion.Substring(0, ProgramVersion.IndexOf(".", ProgramVersion.IndexOf(".") + 1));
        public static readonly string GitHubURL =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTrademarkAttribute>().Trademark;
        public static readonly string Copyright =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;

        /// <summary>
        /// %LOCALAPPDATA%\NutDesk. The parent of the user settings folders, and the data folder unless the data is
        /// kept next to the executable.
        /// </summary>
        public static readonly string LocalDataRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                                                   Environment.SpecialFolderOption.DoNotVerify),
                         LOCAL_DATA_FOLDER_NAME);

        /// <summary>
        /// Where the updater downloads, verifies and stages new versions: %LOCALAPPDATA%\NutDesk\Updates. Always
        /// under %LOCALAPPDATA%, in portable mode too: the files are transient, and the executable's folder is the
        /// very one an update replaces.
        /// </summary>
        public static readonly string UpdatesDirectory = Path.Combine(LocalDataRoot, UPDATES_FOLDER_NAME);

        /// <summary>
        /// Where the logs, the email outbox and the crash reports are written: <see cref="LocalDataRoot"/>, or the
        /// executable's folder when NutDesk was started with <see cref="PARAM_PERSIST_DATA_IN_STARTUP_PATH"/> and
        /// that folder is writable.
        /// </summary>
        public static readonly string DataDirectory;

        /// <summary>
        /// True when <see cref="DataDirectory"/> is the executable's folder (portable mode).
        /// </summary>
        public static readonly bool IsDataInStartupPath;

        /// <summary>
        /// True when NutDesk was started with <see cref="PARAM_PERSIST_DATA_IN_STARTUP_PATH"/>, whether or not the
        /// folder turned out to be writable. What the user asked for is what the autostart entry and the restart
        /// after an update pass on, so the choice survives a run in which the folder happened to be read-only.
        /// </summary>
        public static readonly bool PersistDataInStartupPathRequested;

        public static readonly Logger LogFile = new Logger(LogLvl.LOG_DEBUG);
        public static readonly Updater.UpdateUtil UpdateController = new Updater.UpdateUtil();
        public static List<string> StrLog = new List<string>();

        #endregion

        static AppGlobals()
        {
            PersistDataInStartupPathRequested = Environment.GetCommandLineArgs().Contains(PARAM_PERSIST_DATA_IN_STARTUP_PATH);

            if (PersistDataInStartupPathRequested)
            {
                LogFile.LogTracing("Detected CommandLineArg to store persistent data to StartupPath.", LogLvl.LOG_DEBUG, null);

                if (IsPathWritable(System.Windows.Forms.Application.StartupPath))
                {
                    LogFile.LogTracing("Confirmed StartupPath as chosen path.", LogLvl.LOG_DEBUG, null);
                    DataDirectory = System.Windows.Forms.Application.StartupPath;
                    IsDataInStartupPath = true;
                    return;
                }
                else
                {
                    LogFile.LogTracing("Log to StartupPath requested, but path is not writable.", LogLvl.LOG_ERROR, null);
                }
            }

            LogFile.LogTracing("Setting DataDirectory to preferred location.", LogLvl.LOG_DEBUG, null);
            DataDirectory = LocalDataRoot;

            try
            {
                Directory.CreateDirectory(DataDirectory);
            }
            catch (Exception ex)
            {
                // Everything that writes there creates its own subfolder and copes with failing to; a static
                // constructor that threw would take the whole application down instead.
                LogFile.LogTracing("Unable to create the data folder " + DataDirectory + ": " + ex.Message,
                                   LogLvl.LOG_ERROR, null);
            }
        }

        /// <summary>
        /// Moves the email outbox out of the per-version folder the first public release kept its data in, so the
        /// messages still waiting there are sent after all. It happens once: only while the new location has no
        /// outbox yet. Must run before the notification service opens the outbox. The old log files stay where
        /// they are.
        /// </summary>
        public static void MigrateLegacyData()
        {
            if (IsDataInStartupPath)
            {
                // Portable data never lived in the versioned folder.
                return;
            }

            try
            {
                var target = Path.Combine(DataDirectory, NOTIFICATIONS_FOLDER_NAME, OUTBOX_FILE_NAME);
                var legacyRoot = Path.Combine(LocalDataRoot, LEGACY_PRODUCT_FOLDER_NAME);
                if (File.Exists(target) || !Directory.Exists(legacyRoot))
                {
                    return;
                }

                // Each version folder may hold one; the one written last has the most recent messages.
                var newest = Directory.GetDirectories(legacyRoot)
                                      .Select(dir => new FileInfo(Path.Combine(dir, NOTIFICATIONS_FOLDER_NAME, OUTBOX_FILE_NAME)))
                                      .Where(file => file.Exists)
                                      .OrderByDescending(file => file.LastWriteTimeUtc)
                                      .FirstOrDefault();
                if (newest == null)
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Move(newest.FullName, target);
                LogFile.LogTracing("Email outbox moved from " + newest.FullName + " to " + target + ".", LogLvl.LOG_NOTICE, null);
            }
            catch (Exception ex)
            {
                // The outbox holds notifications about past events; losing them must not stop the client starting.
                LogFile.LogTracing("Unable to move the email outbox from the previous data folder.", LogLvl.LOG_WARNING, null);
                LogFile.LogException(ex, null);
            }
        }

        private static bool IsPathWritable(string _path, bool throwExceptions = false)
        {
            LogFile.LogTracing($"Checking path { _path } for writability...", LogLvl.LOG_DEBUG, null);
            try
            {
                using (var fs = File.Create(Path.Combine(_path, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                {
                }
                LogFile.LogTracing("Path is writable.", LogLvl.LOG_DEBUG, null);
                return true;
            }
            catch
            {
                LogFile.LogTracing("Path is not writable.", LogLvl.LOG_DEBUG, null);
                if (throwExceptions)
                {
                    throw;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
