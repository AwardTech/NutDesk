using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json;
using NutDesk.Common;
using Settings = NutDesk.Properties.Settings;

namespace NutDesk
{
    /// <summary>
    /// Builds and shows the crash dialog, and serialises a bug report the user can paste into an issue.
    /// This was part of the generated <c>MyApplication</c> class in the VB project.
    /// </summary>
    internal static class CrashReporter
    {
        // The report is meant to be pasted into a public issue, so anything that identifies the user's network or
        // accounts stays out of it - the mail account as much as the NUT server.
        private static readonly List<string> SensitiveProperties = new List<string>
        {
            "NUT_ServerAddress", "NUT_ServerPort", "NUT_UPSName", "NUT_Username", "NUT_Password",
            "NT_SmtpHost", "NT_SmtpUser", "NT_SmtpPassword", "NT_EmailFrom", "NT_EmailTo"
        };

        private static Exception caughtException;
        private static Form crashBugForm;

        /// <summary>
        /// Shows the crash dialog for <paramref name="exception"/>.
        /// </summary>
        /// <param name="exception">The exception that brought the application down.</param>
        /// <param name="terminating">
        /// True when the CLR will tear the process down as soon as we return (an unhandled exception on a
        /// thread other than the UI thread). The dialog is then shown modally, because a modeless one would
        /// disappear before it had finished painting.
        /// </param>
        internal static void Report(Exception exception, bool terminating)
        {
            // This builds and shows WinForms controls, so it has to run on the UI thread. Exceptions can
            // reach here from the background connection worker, where every one of those calls would throw.
            // Note MainForm.Instance, which Program assigns to the one form Application.Run is pumping.
            var hostForm = MainForm.Instance;

            if (hostForm != null && hostForm.IsHandleCreated && hostForm.InvokeRequired)
            {
                try
                {
                    hostForm.Invoke(new Action<Exception, bool>(Report), exception, terminating);
                    return;
                }
                catch (Exception marshalEx)
                {
                    AppGlobals.LogFile.LogTracing("Unable to show the crash dialog on the UI thread.", LogLvl.LOG_ERROR, null);
                    AppGlobals.LogFile.LogException(marshalEx, null);
                    return;
                }
            }

            // A second crash while the dialog is already up must not stack another one on top.
            if (crashBugForm != null && !crashBugForm.IsDisposed)
            {
                crashBugForm.BringToFront();
                return;
            }

            caughtException = exception;

            if (hostForm != null && !hostForm.IsDisposed)
            {
                hostForm.HasCrashed = true;
                hostForm.Hide();
            }

            var msgCrash = new Label
            {
                Location = new Point(6, 6),
                Text = CrashText(() => Properties.Resources.CrashDialog_Message,
                       "NutDesk has encountered a critical error and will close soon." + Environment.NewLine +
                       "You can :" + Environment.NewLine +
                       "- generate a crash report which will contain most of the configured parameters (without sensitive" + Environment.NewLine +
                       "  information such as your connection information to your NUT server), the last 50 events logged" + Environment.NewLine +
                       "  and the error message displayed below." + Environment.NewLine +
                       "  This information will Then be copied To your clipboard For easy reporting." + Environment.NewLine +
                       "- simply close NutDesk without generating a report."),
                Size = new Size(470, 100)
            };

            var msgError = new TextBox
            {
                Location = new Point(6, 110),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Text = exception.ToString(),
                Size = new Size(470, 300)
            };

            var btnClose = new Button
            {
                Location = new Point(370, 425),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = CrashText(() => Properties.Resources.CrashDialog_BtnClose, "Close NutDesk"),
                Size = new Size(100, 25)
            };

            var btnGenerate = new Button
            {
                Location = new Point(160, 425),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = CrashText(() => Properties.Resources.CrashDialog_BtnGenerate, "Generate Report and Close NutDesk"),
                Size = new Size(200, 25)
            };

            crashBugForm = new Form
            {
                Icon = Properties.Resources.AppIcon,
                Size = new Size(500, 500),
                FormBorderStyle = FormBorderStyle.Sizable,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                Text = CrashText(() => Properties.Resources.CrashDialog_Caption, "Critical Error Occurred in NutDesk")
            };
            crashBugForm.Controls.Add(msgCrash);
            crashBugForm.Controls.Add(msgError);
            crashBugForm.Controls.Add(btnClose);
            crashBugForm.Controls.Add(btnGenerate);

            btnClose.Click += Close_Button_Click;
            btnGenerate.Click += Generate_Button_Click;

            if (terminating)
            {
                crashBugForm.ShowDialog();
            }
            else
            {
                crashBugForm.Show();
                crashBugForm.BringToFront();
            }
        }

        // The crash dialog is the one screen a non-English user has to read and report from, so its wording
        // comes from the resources. The application has already failed by the time we build it though, and a
        // resource lookup that threw - a damaged or missing satellite assembly, a broken culture - would take
        // down the very report the user is here to produce. Every lookup therefore falls back to the original
        // English wording, which is worth more than an empty or absent dialog.
        private static string CrashText(Func<string> lookup, string fallback)
        {
            try
            {
                var text = lookup();
                return string.IsNullOrEmpty(text) ? fallback : text;
            }
            catch
            {
                return fallback;
            }
        }

        private static string GenerateCrashReport()
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                Culture = Program.DEF_CULTURE_INFO,
                Formatting = Formatting.Indented
            };

            var reportStream = new StringWriter(Program.DEF_CULTURE_INFO);
            reportStream.WriteLine("NutDesk Bug Report");
            reportStream.WriteLine("Generated at " + DateTime.UtcNow.ToString("F", Program.DEF_CULTURE_INFO));
            reportStream.WriteLine();
            reportStream.WriteLine("OS Version: " + Environment.OSVersion.Version);
            reportStream.WriteLine("NutDesk Version: " + AppGlobals.ProgramVersion);

            #region Config output
            reportStream.WriteLine();
            reportStream.WriteLine("==== Settings ====");
            reportStream.WriteLine();

            foreach (SettingsProperty setProp in Settings.Default.Properties)
            {
                string setVal;

                if (SensitiveProperties.Contains(setProp.Name))
                {
                    setVal = "{Removed}";
                    SensitiveProperties.Remove(setProp.Name);
                }
                else
                {
                    // A setting whose stored value cannot be deserialised would otherwise throw from inside
                    // the crash reporter, losing the report the user is here to produce.
                    try
                    {
                        setVal = Convert.ToString(Settings.Default[setProp.Name], Program.DEF_CULTURE_INFO);
                    }
                    catch (Exception ex)
                    {
                        setVal = "{Unreadable: " + ex.GetType().Name + "}";
                    }
                }

                reportStream.WriteLine(setProp.Name + ": " + setVal + " (" + setProp.DefaultValue + ")");
            }
            #endregion

            #region Exceptions
            reportStream.WriteLine("==== Exception ====");
            reportStream.WriteLine();
            reportStream.WriteLine(Regex.Unescape(JsonConvert.SerializeObject(caughtException, jsonSerializerSettings)));
            reportStream.WriteLine();
            #endregion

            reportStream.WriteLine("==== Last Events ====");

            // LastEvents hands back a snapshot, so the reversal and the serialisation have to work on the
            // same list: calling the property twice would serialise an unreversed copy.
            var lastEvents = AppGlobals.LogFile.LastEvents;
            lastEvents.Reverse();
            reportStream.WriteLine();
            reportStream.WriteLine(Regex.Unescape(JsonConvert.SerializeObject(lastEvents, jsonSerializerSettings)));

            return reportStream.ToString();
        }

        private static void Generate_Button_Click(object sender, EventArgs e)
        {
            var logFileName = "CrashReport_" + DateTime.Now.ToString("s").Replace(":", ".") + ".txt";
            var generatedReport = GenerateCrashReport();

            Clipboard.SetText(generatedReport);

            using (var crashLogReport = new StreamWriter(Path.Combine(AppGlobals.DataDirectory, logFileName)))
            {
                crashLogReport.WriteLine(generatedReport);
            }

            // Open an Explorer window to the crash log.
            Process.Start(AppGlobals.DataDirectory);
            Environment.Exit(0);
        }

        private static void Close_Button_Click(object sender, EventArgs e)
        {
            crashBugForm.Close();
        }
    }
}
