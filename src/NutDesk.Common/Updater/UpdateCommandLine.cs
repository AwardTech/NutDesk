using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NutDesk.Common.Updater
{
    /// <summary>
    /// The command lines two versions of NutDesk pass each other during an in-place update.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS A CONTRACT BETWEEN VERSIONS: the side that writes one of these command lines is always a different
    /// version from the side that reads it. The running (old) version starts the new version's staged executable
    /// with
    /// </para>
    /// <code>NutDesk.exe --apply-update --target "&lt;install dir&gt;" --pid &lt;process id&gt; --from &lt;old version&gt; [--persist-data]</code>
    /// <para>
    /// and that new version, once it has copied itself over the install folder, starts the result with
    /// </para>
    /// <code>NutDesk.exe --updated-from &lt;old version&gt; [-PersistDataInStartupPath]</code>
    /// <para>
    /// or, when the copy failed and the old files have been put back, starts the old version again with
    /// </para>
    /// <code>NutDesk.exe --update-failed "&lt;reason&gt;" [-PersistDataInStartupPath]</code>
    /// <para>
    /// So every version, now and in future, must accept --apply-update with exactly these arguments (it may be the
    /// new version of an update started by any older one), and --updated-from and --update-failed (it may be the
    /// version an update comes from). New optional arguments may be added; nothing here may be renamed, removed or
    /// made mandatory, and unknown arguments must keep being ignored.
    /// </para>
    /// </remarks>
    public static class UpdateCommandLine
    {
        /// <summary>Selects the apply mode, which Main handles before anything else.</summary>
        public const string ApplyUpdate = "--apply-update";

        /// <summary>Apply mode: the folder of the installed copy to overwrite.</summary>
        public const string Target = "--target";

        /// <summary>Apply mode: the process id of the running old version, which must exit first.</summary>
        public const string Pid = "--pid";

        /// <summary>Apply mode: the version being replaced, as "1.0.0".</summary>
        public const string From = "--from";

        /// <summary>Apply mode: the old version was started with -PersistDataInStartupPath; restart the same way.</summary>
        public const string PersistData = "--persist-data";

        /// <summary>Normal start: this start follows a successful update from the given version.</summary>
        public const string UpdatedFrom = "--updated-from";

        /// <summary>Normal start: an update failed and this (old) version was restored; the value is the reason.</summary>
        public const string UpdateFailed = "--update-failed";

        // A reason longer than this is cut: it only has to fit a message box, and the whole command line is
        // limited to 32767 characters.
        private const int MAX_REASON_LENGTH = 1000;

        /// <summary>True when the arguments ask for the apply mode.</summary>
        public static bool IsApplyMode(IList<string> args)
        {
            return IndexOf(args, ApplyUpdate) >= 0;
        }

        /// <summary>
        /// The arguments the old version starts the staged new executable with.
        /// </summary>
        public static string BuildApplyArguments(string targetDirectory, int processId, string fromVersion, bool persistData)
        {
            var builder = new StringBuilder();
            builder.Append(ApplyUpdate)
                   .Append(' ').Append(Target).Append(' ').Append(Quote(targetDirectory))
                   .Append(' ').Append(Pid).Append(' ').Append(processId.ToString(CultureInfo.InvariantCulture))
                   .Append(' ').Append(From).Append(' ').Append(Quote(fromVersion));
            if (persistData)
            {
                builder.Append(' ').Append(PersistData);
            }

            return builder.ToString();
        }

        /// <summary>
        /// The arguments the apply mode restarts the updated copy with.
        /// </summary>
        public static string BuildUpdatedArguments(string fromVersion, bool persistData)
        {
            return UpdatedFrom + " " + Quote(fromVersion) + PersistSuffix(persistData);
        }

        /// <summary>
        /// The arguments the apply mode restarts the restored old copy with.
        /// </summary>
        public static string BuildFailedArguments(string reason, bool persistData)
        {
            reason = (reason ?? string.Empty).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (reason.Length > MAX_REASON_LENGTH)
            {
                reason = reason.Substring(0, MAX_REASON_LENGTH);
            }

            return UpdateFailed + " " + Quote(reason) + PersistSuffix(persistData);
        }

        /// <summary>
        /// Reads the apply mode's arguments. Returns null, with the reason in <paramref name="error"/>, when a
        /// required one is missing or malformed.
        /// </summary>
        public static ApplyUpdateArguments ParseApplyArguments(IList<string> args, out string error)
        {
            error = null;

            var target = GetValue(args, Target);
            if (string.IsNullOrWhiteSpace(target))
            {
                error = "missing " + Target;
                return null;
            }

            var pidText = GetValue(args, Pid);
            if (!int.TryParse(pidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid) || pid <= 0)
            {
                error = "missing or invalid " + Pid;
                return null;
            }

            var from = GetValue(args, From);
            if (string.IsNullOrWhiteSpace(from))
            {
                error = "missing " + From;
                return null;
            }

            return new ApplyUpdateArguments(target.Trim(), pid, from.Trim(), IndexOf(args, PersistData) >= 0);
        }

        /// <summary>
        /// The value following <paramref name="name"/> in the arguments, or null when the switch is absent or last.
        /// Switch names are compared without regard to case.
        /// </summary>
        public static string GetValue(IList<string> args, string name)
        {
            var index = IndexOf(args, name);
            return index >= 0 && index + 1 < args.Count ? args[index + 1] : null;
        }

        /// <summary>
        /// Quotes one argument so that the Windows command-line parser (CommandLineToArgvW, and the CLR's own) gives
        /// it back unchanged: backslashes before a quote - a folder such as "C:\" ends with one - are doubled.
        /// </summary>
        public static string Quote(string value)
        {
            value = value ?? string.Empty;

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');

            int backslashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    // Each backslash before a quote is doubled, and one more escapes the quote itself.
                    builder.Append('\\', backslashes * 2 + 1).Append('"');
                }
                else
                {
                    builder.Append('\\', backslashes).Append(c);
                }

                backslashes = 0;
            }

            // Before the closing quote, trailing backslashes must be doubled or the last one would escape it.
            builder.Append('\\', backslashes * 2).Append('"');
            return builder.ToString();
        }

        private static string PersistSuffix(bool persistData)
        {
            return persistData ? " " + AppGlobals.PARAM_PERSIST_DATA_IN_STARTUP_PATH : string.Empty;
        }

        private static int IndexOf(IList<string> args, string name)
        {
            if (args == null)
            {
                return -1;
            }

            for (int i = 0; i < args.Count; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// The apply mode's arguments, as read by <see cref="UpdateCommandLine.ParseApplyArguments"/>.
    /// </summary>
    public sealed class ApplyUpdateArguments
    {
        internal ApplyUpdateArguments(string targetDirectory, int processId, string fromVersion, bool persistData)
        {
            TargetDirectory = targetDirectory;
            ProcessId = processId;
            FromVersion = fromVersion;
            PersistData = persistData;
        }

        /// <summary>The install folder to overwrite.</summary>
        public string TargetDirectory { get; }

        /// <summary>The old version's process, which must have exited before anything is copied.</summary>
        public int ProcessId { get; }

        /// <summary>The version being replaced, passed on as --updated-from.</summary>
        public string FromVersion { get; }

        /// <summary>Restart with -PersistDataInStartupPath.</summary>
        public bool PersistData { get; }
    }
}
