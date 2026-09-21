using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NutDesk.Common;
using NutDesk.Common.Updater;

namespace NutDesk.Updating
{
    /// <summary>
    /// The apply mode of an in-place update: this executable, started by the previous version from the staging
    /// folder the update was unpacked into, copies itself over the installed copy and restarts it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The command line it is started with, and the ones it restarts NutDesk with, are a contract between versions;
    /// see <see cref="UpdateCommandLine"/>.
    /// </para>
    /// <para>
    /// Program.Main enters it before the single-instance mutex, the settings, the notification service or any
    /// window exist, and it touches none of them: it is a file copier that writes its own log
    /// (UpdatesDirectory\apply.log) and shows nothing but an error message when something goes wrong. Every file it
    /// is about to overwrite is first saved into Updates\&lt;tag&gt;\backup; if any copy fails, the saved files are
    /// put back, the files it added are removed, and the old version is started again with --update-failed.
    /// </para>
    /// </remarks>
    internal static class ApplyUpdateMode
    {
        // How long the old version gets to exit after starting this one. It saves its settings and flushes its
        // email outbox on the way out, which takes seconds, not a minute.
        private const int WAIT_FOR_EXIT_SECONDS = 60;

        // A virus scanner or the search indexer can hold a file that has just been closed for a moment.
        private const int COPY_ATTEMPTS = 5;
        private static readonly TimeSpan RETRY_DELAY = TimeSpan.FromMilliseconds(700);

        private const string LOG_FILE_NAME = "apply.log";

        // The log is appended to on every update; past this size it starts over.
        private const long MAX_LOG_BYTES = 1024 * 1024;

        private static StreamWriter log;

        /// <summary>
        /// Runs the apply mode and returns the process exit code: 0 when the update was installed.
        /// </summary>
        internal static int Run(IList<string> args)
        {
            Application.EnableVisualStyles();
            OpenLog();

            try
            {
                return Apply(args);
            }
            catch (Exception ex)
            {
                // Every expected failure is handled on the way; this is the net under the unexpected ones.
                Log("Unexpected failure: " + ex);
                ShowError(UpdateText.Format(Properties.Resources.Update_InstallFailed, ex.Message));
                return 1;
            }
            finally
            {
                Log("Apply mode finished.");
                CloseLog();
            }
        }

        private static int Apply(IList<string> args)
        {
            var source = Path.GetDirectoryName(Application.ExecutablePath);
            var newVersion = UpdateUtil.RunningVersionText;
            Log($"NutDesk {newVersion} started in apply mode from {source}. Arguments: {string.Join(" ", args)}");

            var arguments = UpdateCommandLine.ParseApplyArguments(args, out string argumentError);
            if (arguments == null)
            {
                Log("Refused: " + argumentError + ".");
                ShowError(UpdateText.Format(Properties.Resources.Update_Apply_BadArguments, argumentError));
                return 2;
            }

            string target;
            try
            {
                target = Path.GetFullPath(arguments.TargetDirectory);
            }
            catch (Exception ex)
            {
                Log($"Refused: the target '{arguments.TargetDirectory}' is not a usable path ({ex.Message}).");
                ShowError(UpdateText.Format(Properties.Resources.Update_Apply_InvalidTarget, arguments.TargetDirectory));
                return 3;
            }

            // Only a folder that holds NutDesk is ever written to, and never the folder this copy runs from.
            if (!File.Exists(Path.Combine(target, UpdateInstaller.ExecutableName)) || SamePath(target, source))
            {
                Log($"Refused: {target} holds no {UpdateInstaller.ExecutableName}, or is the folder this copy runs from.");
                ShowError(UpdateText.Format(Properties.Resources.Update_Apply_InvalidTarget, target));
                return 3;
            }

            Log($"Waiting for process {arguments.ProcessId} (NutDesk {arguments.FromVersion}) to exit.");
            if (!WaitForExit(arguments.ProcessId))
            {
                // Nothing has been touched, and the old version is still running: there is nothing to restart.
                Log($"It did not exit within {WAIT_FOR_EXIT_SECONDS} seconds; nothing has been changed.");
                ShowError(UpdateText.Format(Properties.Resources.Update_Apply_Timeout, WAIT_FOR_EXIT_SECONDS));
                return 4;
            }

            var files = ListFiles(source);
            var backup = GetBackupDirectory(source, newVersion);
            Log($"Installing {files.Count} files from {source} into {target}; replaced files are saved in {backup}.");

            var backedUp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var written = new List<string>();

            if (!TryBackup(target, backup, files, backedUp, out string failure) ||
                !TryCopy(source, target, files, written, out failure))
            {
                Log("Install failed: " + failure);

                string reason;
                var restoreFailure = Restore(target, backup, written, backedUp);
                if (restoreFailure == null)
                {
                    Log("The previous version has been restored.");
                    reason = UpdateText.Format(Properties.Resources.Update_Apply_CopyFailed, target, failure);
                }
                else
                {
                    Log("The previous version could not be completely restored: " + restoreFailure);
                    reason = UpdateText.Format(Properties.Resources.Update_Apply_RestoreFailed, restoreFailure, backup, target);

                    // Said here too: what is left in the folder may not even start to say it.
                    ShowError(reason);
                }

                Relaunch(target, UpdateCommandLine.BuildFailedArguments(reason, arguments.PersistData));
                return 5;
            }

            Log($"NutDesk {arguments.FromVersion} updated to {newVersion} in {target}.");
            Relaunch(target, UpdateCommandLine.BuildUpdatedArguments(arguments.FromVersion, arguments.PersistData));
            return 0;
        }

        #region Steps

        /// <summary>
        /// Waits for the old version to exit. True when it has (or was already gone).
        /// </summary>
        private static bool WaitForExit(int processId)
        {
            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return true;
            }

            using (process)
            {
                try
                {
                    return process.WaitForExit(WAIT_FOR_EXIT_SECONDS * 1000);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                {
                    // Not a process this user may wait for, so not the NutDesk that started this copy: the id has
                    // been reused. A copy still running would make the copying fail, and that is handled.
                    Log($"Cannot wait for process {processId}: {ex.Message}");
                    return true;
                }
            }
        }

        /// <summary>
        /// The files of the new version, as paths relative to <paramref name="source"/>.
        /// </summary>
        private static List<string> ListFiles(string source)
        {
            var prefix = source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Directory.GetFiles(source, "*", SearchOption.AllDirectories)
                            .Select(file => file.Substring(prefix.Length))
                            .OrderBy(relative => relative, StringComparer.OrdinalIgnoreCase)
                            .ToList();
        }

        /// <summary>
        /// Updates\&lt;tag&gt;\backup for a copy running from Updates\&lt;tag&gt;\staging\..., the folder
        /// <see cref="UpdateInstaller"/> unpacks into. Anywhere else, a folder named after the version.
        /// </summary>
        private static string GetBackupDirectory(string source, string version)
        {
            for (var folder = new DirectoryInfo(source); folder?.Parent != null; folder = folder.Parent)
            {
                if (string.Equals(folder.Name, UpdateInstaller.StagingFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(folder.Parent.FullName, UpdateInstaller.BackupFolderName);
                }
            }

            return Path.Combine(AppGlobals.UpdatesDirectory, version, UpdateInstaller.BackupFolderName);
        }

        /// <summary>
        /// Saves every file of the target that the new version will overwrite. Nothing in the target changes here,
        /// so a failure needs no restoring.
        /// </summary>
        private static bool TryBackup(string target, string backup, IList<string> files, ISet<string> backedUp, out string failure)
        {
            failure = null;

            try
            {
                // A backup left by an earlier failed attempt describes an older state of the folder.
                if (!UpdateInstaller.TryDeleteDirectory(backup))
                {
                    throw new IOException("The previous backup folder " + backup + " could not be emptied.");
                }

                Directory.CreateDirectory(backup);

                foreach (var relative in files)
                {
                    var current = Path.Combine(target, relative);
                    if (!File.Exists(current))
                    {
                        continue;
                    }

                    var saved = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved));
                    WithRetries(() => File.Copy(current, saved, true), "back up " + relative);
                    backedUp.Add(relative);
                }

                Log($"{backedUp.Count} files backed up.");
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Copies the new version over the target, creating subfolders (it-IT\ and the like) as needed.
        /// </summary>
        private static bool TryCopy(string source, string target, IList<string> files, IList<string> written, out string failure)
        {
            failure = null;

            foreach (var relative in files)
            {
                try
                {
                    var destination = Path.Combine(target, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));

                    // Recorded before copying: a copy that breaks half way can leave a damaged file behind, and that
                    // one has to be restored as well.
                    written.Add(relative);
                    WithRetries(() => CopyOver(Path.Combine(source, relative), destination), "install " + relative);
                }
                catch (Exception ex)
                {
                    // The message already names the file, in full; it becomes part of what the user reads.
                    Log($"Could not install {relative}.");
                    failure = ex.Message;
                    return false;
                }
            }

            Log($"{written.Count} files installed.");
            return true;
        }

        /// <summary>
        /// Undoes <see cref="TryCopy"/>: puts the saved files back and removes the ones the new version added.
        /// Returns null when everything was restored, otherwise what could not be.
        /// </summary>
        private static string Restore(string target, string backup, IList<string> written, ISet<string> backedUp)
        {
            var problems = new List<string>();

            foreach (var relative in written.Reverse())
            {
                var destination = Path.Combine(target, relative);
                try
                {
                    if (backedUp.Contains(relative))
                    {
                        WithRetries(() => CopyOver(Path.Combine(backup, relative), destination), "restore " + relative);
                    }
                    else if (File.Exists(destination))
                    {
                        WithRetries(() =>
                        {
                            File.SetAttributes(destination, FileAttributes.Normal);
                            File.Delete(destination);
                        }, "remove " + relative);
                    }
                }
                catch (Exception ex)
                {
                    problems.Add(relative + ": " + ex.Message);
                }
            }

            return problems.Count == 0 ? null : string.Join("; ", problems);
        }

        /// <summary>
        /// Starts NutDesk from the target folder with the given arguments. Through the shell, as the old version
        /// started this one: a process created directly inherits every inheritable handle of its parent, and on the
        /// .NET Framework that includes the old version's socket to the NUT server, which would then stay open for
        /// as long as the new version runs.
        /// </summary>
        private static void Relaunch(string target, string arguments)
        {
            var executable = Path.Combine(target, UpdateInstaller.ExecutableName);

            try
            {
                var startInfo = new ProcessStartInfo(executable, arguments)
                {
                    UseShellExecute = true,
                    WorkingDirectory = target,
                };

                using (Process.Start(startInfo))
                {
                }

                Log($"Started {executable} {arguments}");
            }
            catch (Exception ex)
            {
                Log($"Could not start {executable}: {ex.Message}");
                ShowError(UpdateText.Format(Properties.Resources.Update_Apply_RestartFailed, ex.Message, executable));
            }
        }

        #endregion

        #region Helpers

        private static void WithRetries(Action action, string what)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < COPY_ATTEMPTS)
                {
                    Log($"Could not {what} (attempt {attempt} of {COPY_ATTEMPTS}): {ex.Message}");
                    Thread.Sleep(RETRY_DELAY);
                }
            }
        }

        /// <summary>
        /// File.Copy with overwrite, which refuses a read-only or hidden destination; those attributes are cleared.
        /// </summary>
        private static void CopyOver(string source, string destination)
        {
            if (File.Exists(destination))
            {
                var attributes = File.GetAttributes(destination);
                var blocking = FileAttributes.ReadOnly | FileAttributes.Hidden;
                if ((attributes & blocking) != 0)
                {
                    File.SetAttributes(destination, attributes & ~blocking);
                }
            }

            File.Copy(source, destination, true);
        }

        private static bool SamePath(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                                 Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                                 StringComparison.OrdinalIgnoreCase);
        }

        private static void ShowError(string message)
        {
            Log("Message shown: " + message);

            try
            {
                MessageBox.Show(message, Properties.Resources.Update_Apply_Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                Log("The message could not be shown: " + ex.Message);
            }
        }

        private static void OpenLog()
        {
            try
            {
                Directory.CreateDirectory(AppGlobals.UpdatesDirectory);

                var path = Path.Combine(AppGlobals.UpdatesDirectory, LOG_FILE_NAME);
                var existing = new FileInfo(path);
                var mode = existing.Exists && existing.Length > MAX_LOG_BYTES ? System.IO.FileMode.Create : System.IO.FileMode.Append;

                // With a byte order mark, which StreamWriter only writes into an empty file, so that editors
                // defaulting to the ANSI code page still read the translated messages right.
                log = new StreamWriter(new FileStream(path, mode, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(true))
                {
                    AutoFlush = true,
                };
                log.WriteLine();
            }
            catch (Exception ex)
            {
                // A log that cannot be written is no reason to leave the user without the update.
                Trace.WriteLine("[apply] The apply log cannot be written: " + ex.Message);
                log = null;
            }
        }

        private static void Log(string message)
        {
            Trace.WriteLine("[apply] " + message);

            try
            {
                log?.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message);
            }
            catch (Exception)
            {
                // See OpenLog.
            }
        }

        private static void CloseLog()
        {
            try
            {
                log?.Dispose();
            }
            catch (Exception)
            {
                // Nothing left to report it to.
            }

            log = null;
        }

        #endregion
    }
}
