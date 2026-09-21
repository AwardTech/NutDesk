using System;
using System.Windows.Forms;
using Microsoft.Win32;
using NutDesk.Common;

namespace NutDesk
{
    /// <summary>
    /// The "Start with Windows" entry: a value named after the product under
    /// HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
    /// </summary>
    /// <remarks>
    /// The entry holds an executable path, and NutDesk is a portable program: the user can move its folder, or put a
    /// new version in another one. An entry that kept naming the old path would start the old copy at logon (whose
    /// single-instance mutex then swallows the new one), or nothing at all once the old folder is gone. So the entry
    /// follows the copy that is running: it is checked at every start (<see cref="Reconcile"/>) and rewritten on
    /// every save of the preferences (<see cref="Apply"/>).
    /// </remarks>
    internal static class AutoStart
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>The value name, which the existing entries have always been written under.</summary>
        private static string ValueName => Application.ProductName;

        /// <summary>
        /// The command the entry should hold: this executable, quoted because the path can contain spaces, plus
        /// -PersistDataInStartupPath when this copy was started with it, so a copy that keeps its data next to itself
        /// does so after a logon too.
        /// </summary>
        internal static string ExpectedCommand =>
            "\"" + Application.ExecutablePath + "\"" +
            (AppGlobals.PersistDataInStartupPathRequested ? " " + AppGlobals.PARAM_PERSIST_DATA_IN_STARTUP_PATH : string.Empty);

        /// <summary>
        /// Writes the entry (always, so it is repaired if it named another copy) or removes it, to match the
        /// setting. Throws when the registry cannot be written; the preferences report that as a failed save.
        /// </summary>
        internal static void Apply(bool enabled)
        {
            if (enabled)
            {
                Write();
                AppGlobals.LogFile.LogTracing("Start with Windows entry written: " + ExpectedCommand, LogLvl.LOG_DEBUG, null);
            }
            else if (Remove())
            {
                AppGlobals.LogFile.LogTracing("Start with Windows entry removed.", LogLvl.LOG_DEBUG, null);
            }
        }

        /// <summary>
        /// At startup: when "Start with Windows" is on and the entry is missing or starts something other than this
        /// copy, rewrite it. Leaves the registry alone when the setting is off. Never throws.
        /// </summary>
        internal static void Reconcile(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            try
            {
                var current = Read();
                var expected = ExpectedCommand;

                // Paths are not case-sensitive; only a real difference is worth a registry write.
                if (string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Write();
                AppGlobals.LogFile.LogTracing("Start with Windows entry " +
                                              (current == null ? "was missing" : "started " + current) +
                                              "; rewritten to " + expected + ".", LogLvl.LOG_NOTICE, null);
            }
            catch (Exception ex)
            {
                AppGlobals.LogFile.LogTracing("Unable to check the Start with Windows entry.", LogLvl.LOG_WARNING, null);
                AppGlobals.LogFile.LogException(ex, null);
            }
        }

        private static string Read()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key?.GetValue(ValueName) as string;
            }
        }

        private static void Write()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                key.SetValue(ValueName, ExpectedCommand, RegistryValueKind.String);
            }
        }

        /// <summary>Deletes the entry. True when there was one.</summary>
        private static bool Remove()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key?.GetValue(ValueName) == null)
                {
                    return false;
                }

                key.DeleteValue(ValueName, false);
                return true;
            }
        }
    }
}
