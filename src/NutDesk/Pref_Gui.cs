// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using NutDesk.Common;
using LogLvl = NutDesk.Common.LogLvl;
using static NutDesk.Common.AppGlobals;

namespace NutDesk
{
    public partial class Pref_Gui : Form
    {
        // VB reached this form through its default instance (Pref_Gui.ShowDialog()); this reproduces the
        // "recreate it if it was closed" behaviour explicitly.
        private static Pref_Gui _instance;

        internal static Pref_Gui Instance =>
            (_instance == null || _instance.IsDisposed) ? (_instance = new Pref_Gui()) : _instance;

        private bool IsShowed = false;
        private bool PrefsModified = false;

        // Subscribers attach to this once, at startup, and expect to keep receiving notifications even
        // after the dialog has been closed and recreated - hence static (VB reached it through the
        // default instance, which hid the distinction).
        public static event Action SavedPreferences;

        public Pref_Gui()
        {
            InitializeComponent();
            WireEvents();
        }

        /// <summary>
        /// Replaces the VB "Handles" clauses. All event wiring lives here, never in the designer file.
        /// </summary>
        private void WireEvents()
        {
            Load += Pref_Gui_Load;
            Shown += Pref_Gui_Shown;
            FormClosing += Pref_Gui_FormClosing;

            Btn_Cancel.Click += Btn_Cancel_Click;
            Btn_Apply.Click += Btn_Apply_Click;
            Btn_Ok.Click += Btn_Ok_Click;
            Btn_ViewLog.Click += Btn_ViewLog_Click;
            Btn_DeleteLog.Click += Btn_DeleteLog_Click;

            CB_Systray.CheckedChanged += CB_Systray_CheckedChanged;
            Cb_ImmediateStop.CheckedChanged += Cb_ImmediateStop_CheckedChanged;
            Cb_ExtendTime.CheckedChanged += Cb_ExtendTime_CheckedChanged;
            Cb_Update_At_Start.CheckedChanged += Cb_Update_At_Start_CheckedChanged;

            TabControl_Options.Selecting += TabControl_Options_Selecting;

            Tb_Port.Validating += Number_Validating;
            Tb_OutV_Min.Validating += Number_Validating;
            Tb_OutV_Max.Validating += Number_Validating;
            Tb_InV_Min.Validating += Number_Validating;
            Tb_InV_Max.Validating += Number_Validating;
            Tb_InF_Min.Validating += Number_Validating;
            Tb_InF_Max.Validating += Number_Validating;
            Tb_GraceTime.Validating += Number_Validating;
            Tb_Delay_Stop.Validating += Number_Validating;
            Tb_BattV_Min.Validating += Number_Validating;
            Tb_BattV_Max.Validating += Number_Validating;
            Tb_BattLimit_Time.Validating += Number_Validating;
            Tb_BattLimit_Load.Validating += Number_Validating;

            Tb_Server_IP.Validating += Correct_IP_Validating;
        }

        private void Btn_Cancel_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Close Pref Gui from Button Cancel", LogLvl.LOG_DEBUG, this);
            Close();
        }

        private void Save_Params()
        {
            try
            {
                LogFile.LogTracing("Save Parameters.", LogLvl.LOG_DEBUG, this);

                // Each field is only range checked on its own, so an inverted or empty calibration range
                // (240/200, or 230/230) used to be saved happily and then left the matching gauge stuck at
                // empty forever. Refuse the whole save until every pair makes sense.
                bool InVRangeOk = IsCalibrationRangeValid(Tb_InV_Min, Tb_InV_Max);
                bool InFRangeOk = IsCalibrationRangeValid(Tb_InF_Min, Tb_InF_Max);
                bool OutVRangeOk = IsCalibrationRangeValid(Tb_OutV_Min, Tb_OutV_Max);
                bool BattVRangeOk = IsCalibrationRangeValid(Tb_BattV_Min, Tb_BattV_Max);

                if (!(InVRangeOk && InFRangeOk && OutVRangeOk && BattVRangeOk))
                {
                    LogFile.LogTracing("Preferences not saved. At least one calibration range is invalid.", LogLvl.LOG_ERROR, this);
                    MessageBox.Show(Properties.Resources.Pref_Gui_RangeErrorMessage, Properties.Resources.Pref_Gui_RangeErrorCaption,
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    // PrefsModified stays set on purpose. Nothing was written, so Btn_Apply has to remain
                    // available and Btn_Ok must not treat this as a completed save.
                    return;
                }

                Properties.Settings.Default.NUT_ServerAddress = Tb_Server_IP.Text;
                Properties.Settings.Default.NUT_ServerPort = Convert.ToInt32(Tb_Port.Text);
                Properties.Settings.Default.NUT_UPSName = Tb_UPS_Name.Text;
                Properties.Settings.Default.NUT_PollIntervalMsec = Convert.ToInt32(pollingIntervalValue.Value * 1000m);
                Properties.Settings.Default.NUT_Username = (SerializedProtectedString)Tb_Login_Nut.Text;
                Properties.Settings.Default.NUT_Password = (SerializedProtectedString)Tb_Pwd_Nut.Text;
                Properties.Settings.Default.NUT_AutoReconnect = Cb_Reconnect.Checked;
                Properties.Settings.Default.CAL_VoltInMin = Convert.ToInt32(Tb_InV_Min.Text);
                Properties.Settings.Default.CAL_VoltInMax = Convert.ToInt32(Tb_InV_Max.Text);
                Properties.Settings.Default.CAL_FreqInNom = Convert.ToInt32(Cbx_Freq_Input.SelectedItem);
                Properties.Settings.Default.CAL_FreqInMin = Convert.ToInt32(Tb_InF_Min.Text);
                Properties.Settings.Default.CAL_FreqInMax = Convert.ToInt32(Tb_InF_Max.Text);
                Properties.Settings.Default.CAL_VoltOutMin = Convert.ToInt32(Tb_OutV_Min.Text);
                Properties.Settings.Default.CAL_VoltOutMax = Convert.ToInt32(Tb_OutV_Max.Text);
                Properties.Settings.Default.CAL_BattVMin = Convert.ToInt32(Tb_BattV_Min.Text);
                Properties.Settings.Default.CAL_BattVMax = Convert.ToInt32(Tb_BattV_Max.Text);
                Properties.Settings.Default.MinimizeToTray = CB_Systray.Checked;
                Properties.Settings.Default.MinimizeOnStart = CB_Start_Mini.Checked;
                Properties.Settings.Default.CloseToTray = CB_Close_Tray.Checked;
                Properties.Settings.Default.StartWithWindows = CB_Start_W_Win.Checked;
                Properties.Settings.Default.LG_LogToFile = CB_Use_Logfile.Checked;
                Properties.Settings.Default.LG_LogLevel = Cbx_LogLevel.SelectedIndex;
                Properties.Settings.Default.PW_BattChrgFloor = Convert.ToInt32(Tb_BattLimit_Load.Text);
                Properties.Settings.Default.PW_RuntimeFloor = Convert.ToInt32(Tb_BattLimit_Time.Text);
                Properties.Settings.Default.PW_Immediate = Cb_ImmediateStop.Checked;
                Properties.Settings.Default.PW_RespectFSD = CB_Follow_FSD.Checked;
                Properties.Settings.Default.PW_StopType = Cbx_TypeStop.SelectedIndex;
                Properties.Settings.Default.PW_StopDelaySec = Convert.ToInt32(Tb_Delay_Stop.Text);
                Properties.Settings.Default.PW_UserExtendStopTimer = Cb_ExtendTime.Checked;
                Properties.Settings.Default.PW_ExtendDelaySec = Convert.ToInt32(Tb_GraceTime.Text);
                Properties.Settings.Default.UP_CheckAtStart = Cb_Update_At_Start.Checked;
                Properties.Settings.Default.UP_AutoChkDelay = Cbx_Delay_Verif.SelectedIndex;
                Properties.Settings.Default.UP_Branch = Cbx_Branch_Update.SelectedIndex;

                Properties.Settings.Default.Save();
                // The same entry the preferences window writes; see AutoStart.
                AutoStart.Apply(CB_Start_W_Win.Checked);

                SavedPreferences?.Invoke();

                SetLogControlsStatus();
                LogFile.LogTracing("NutDesk Preferences Saved.", LogLvl.LOG_NOTICE, this, StrLog[(int)AppResxStr.STR_LOG_PREFS]);

                PrefsModified = false;
            }
            catch (Exception e)
            {
                LogFile.LogTracing("Error when trying to save preferences.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(e, this);
                // A stack trace is nothing an end user can act on, and an English literal caption cannot be
                // translated. The exception itself is already in the log, which is where the detail belongs.
                MessageBox.Show(Properties.Resources.Pref_Gui_SaveErrorMessage, Properties.Resources.Pref_Gui_SaveErrorCaption,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Check a calibration min/max pair, colouring both fields the way Number_Validating does.
        /// </summary>
        /// <returns>True when the minimum is strictly lower than the maximum.</returns>
        private bool IsCalibrationRangeValid(TextBox MinBox, TextBox MaxBox)
        {
            int MinValue, MaxValue;

            // Number_Validating only ever sees one field at a time and so cannot tell that the two together
            // describe a span no reading can fall into, which is what empties the gauge.
            if (int.TryParse(MinBox.Text, out MinValue) && int.TryParse(MaxBox.Text, out MaxValue) && MinValue < MaxValue)
            {
                // Clear an earlier complaint so the highlight disappears as soon as the pair is corrected.
                MinBox.BackColor = Color.White;
                MaxBox.BackColor = Color.White;
                return true;
            }

            LogFile.LogTracing(string.Format("Calibration range {0}/{1} is invalid. The minimum has to be lower than the maximum.", MinBox.Name, MaxBox.Name), LogLvl.LOG_ERROR, this);
            MinBox.BackColor = Color.Red;
            MaxBox.BackColor = Color.Red;
            return false;
        }

        private void Btn_Apply_Click(object sender, EventArgs e)
        {
            Save_Params();
            if (!PrefsModified)
            {
                Btn_Apply.Enabled = false;
            }
        }

        private void Btn_Ok_Click(object sender, EventArgs e)
        {
            if (PrefsModified)
            {
                Save_Params();
                // Save_Params leaves PrefsModified set when it refused to save. Closing anyway would throw
                // away every other edit the user made along with the highlighted fields they have to fix.
                if (PrefsModified)
                {
                    return;
                }
            }
            Close();
        }

        private void Pref_Gui_Shown(object sender, EventArgs e)
        {
            try
            {
                IsShowed = false;
                Tb_Server_IP.Text = Properties.Settings.Default.NUT_ServerAddress;
                Tb_Port.Text = Properties.Settings.Default.NUT_ServerPort.ToString();
                Tb_UPS_Name.Text = Properties.Settings.Default.NUT_UPSName;
                pollingIntervalValue.Value = Properties.Settings.Default.NUT_PollIntervalMsec / 1000m;
                Tb_Login_Nut.Text = Properties.Settings.Default.NUT_Username;
                Tb_Pwd_Nut.Text = Properties.Settings.Default.NUT_Password;
                Cb_Reconnect.Checked = Properties.Settings.Default.NUT_AutoReconnect;
                Tb_InV_Min.Text = Properties.Settings.Default.CAL_VoltInMin.ToString();
                Tb_InV_Max.Text = Properties.Settings.Default.CAL_VoltInMax.ToString();
                Cbx_Freq_Input.SelectedIndex = Cbx_Freq_Input.FindStringExact(Properties.Settings.Default.CAL_FreqInNom.ToString());
                Tb_InF_Min.Text = Properties.Settings.Default.CAL_FreqInMin.ToString();
                Tb_InF_Max.Text = Properties.Settings.Default.CAL_FreqInMax.ToString();
                Tb_OutV_Min.Text = Properties.Settings.Default.CAL_VoltOutMin.ToString();
                Tb_OutV_Max.Text = Properties.Settings.Default.CAL_VoltOutMax.ToString();
                Tb_BattV_Min.Text = Properties.Settings.Default.CAL_BattVMin.ToString();
                Tb_BattV_Max.Text = Properties.Settings.Default.CAL_BattVMax.ToString();
                CB_Systray.Checked = Properties.Settings.Default.MinimizeToTray;
                CB_Start_Mini.Checked = Properties.Settings.Default.MinimizeOnStart;
                CB_Close_Tray.Checked = Properties.Settings.Default.CloseToTray;
                CB_Start_W_Win.Checked = Properties.Settings.Default.StartWithWindows;
                CB_Use_Logfile.Checked = Properties.Settings.Default.LG_LogToFile;
                Cbx_LogLevel.SelectedIndex = Properties.Settings.Default.LG_LogLevel;
                Tb_BattLimit_Load.Text = Properties.Settings.Default.PW_BattChrgFloor.ToString();
                Tb_BattLimit_Time.Text = Properties.Settings.Default.PW_RuntimeFloor.ToString();
                Cb_ImmediateStop.Checked = Properties.Settings.Default.PW_Immediate;
                CB_Follow_FSD.Checked = Properties.Settings.Default.PW_RespectFSD;
                Cbx_TypeStop.SelectedIndex = Properties.Settings.Default.PW_StopType;
                Tb_Delay_Stop.Text = Properties.Settings.Default.PW_StopDelaySec.ToString();
                Cb_ExtendTime.Checked = Properties.Settings.Default.PW_UserExtendStopTimer;
                Tb_GraceTime.Text = Properties.Settings.Default.PW_ExtendDelaySec.ToString();
                Cb_Update_At_Start.Checked = Properties.Settings.Default.UP_CheckAtStart;
                Cbx_Delay_Verif.SelectedIndex = Properties.Settings.Default.UP_AutoChkDelay;
                Cbx_Branch_Update.SelectedIndex = Properties.Settings.Default.UP_Branch;
                if (CB_Systray.Checked)
                {
                    CB_Start_Mini.Enabled = true;
                    CB_Close_Tray.Enabled = true;
                }
                else
                {
                    CB_Start_Mini.Enabled = false;
                    CB_Close_Tray.Enabled = false;
                }
                if (Cb_ImmediateStop.Checked)
                {
                    Tb_Delay_Stop.Enabled = false;
                }
                else
                {
                    Tb_Delay_Stop.Enabled = true;
                }
                if (Cb_ExtendTime.Checked)
                {
                    Tb_GraceTime.Enabled = true;
                }
                else
                {
                    Tb_GraceTime.Enabled = false;
                }

                Cb_Update_At_Start_CheckedChanged(this, EventArgs.Empty);

                foreach (var TabCtrl in TabControl_Options.Controls.OfType<TabPage>())
                {
                    var TBoxes = TabCtrl.Controls.OfType<TextBox>();
                    var ChkBoxes = TabCtrl.Controls.OfType<CheckBox>();
                    var CmbBoxes = TabCtrl.Controls.OfType<ComboBox>();
                    foreach (var TBox in TBoxes)
                    {
                        TBox.TextChanged += Event_Ctrl_Value_Changed;
                    }
                    foreach (var ChkBox in ChkBoxes)
                    {
                        ChkBox.CheckedChanged += Event_Ctrl_Value_Changed;
                    }
                    foreach (var CmbBox in CmbBoxes)
                    {
                        CmbBox.SelectedIndexChanged += Event_Ctrl_Value_Changed;
                    }
                }
                pollingIntervalValue.ValueChanged += Event_Ctrl_Value_Changed;

                SetLogControlsStatus();
                IsShowed = true;
                LogFile.LogTracing("Pref Gui Opened.", LogLvl.LOG_DEBUG, this);
            }
            catch (Exception Except)
            {
                IsShowed = false;
                Close();
                LogFile.LogTracing("Error on Opening Pref_Gui:" + Environment.NewLine + Except.ToString(), LogLvl.LOG_ERROR, this);
            }
        }

        private void CB_Systray_CheckedChanged(object sender, EventArgs e)
        {
            if (CB_Systray.Checked)
            {
                CB_Start_Mini.Enabled = true;
                CB_Close_Tray.Enabled = true;
            }
            else
            {
                CB_Start_Mini.Enabled = false;
                CB_Close_Tray.Enabled = false;
            }
        }

        private void Cb_ImmediateStop_CheckedChanged(object sender, EventArgs e)
        {
            if (Cb_ImmediateStop.Checked)
            {
                Tb_Delay_Stop.Enabled = false;
            }
            else
            {
                Tb_Delay_Stop.Enabled = true;
                Number_Validating(Tb_Delay_Stop, new System.ComponentModel.CancelEventArgs());
            }
        }

        private void Cb_ExtendTime_CheckedChanged(object sender, EventArgs e)
        {
            if (Cb_ExtendTime.Checked)
            {
                Tb_GraceTime.Enabled = true;
                Number_Validating(Tb_GraceTime, new System.ComponentModel.CancelEventArgs());
            }
            else
            {
                Tb_GraceTime.Enabled = false;
            }
        }

        private void Cb_Update_At_Start_CheckedChanged(object sender, EventArgs e)
        {
            Cbx_Delay_Verif.Enabled = Cb_Update_At_Start.Checked;
        }

        private void Number_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsShowed)
            {
                // VB reached these through late binding on the Object parameter; every control that this
                // routine is wired to (and every direct caller) passes a TextBox.
                var control = (TextBox)sender;
                string StrTest = control.Text;
                int Result = 0;
                int MinValue = 0, MaxValue = 0;

                LogFile.LogTracing(string.Format("Check that the value of {0} for {1} is correct.", control.Text, control.Name), LogLvl.LOG_DEBUG, this);
                switch (control.Name)
                {
                    case "Tb_Port":
                        MinValue = 1;
                        MaxValue = 65536;
                        break;
                    case "Tb_OutV_Min":
                    case "Tb_OutV_Max":
                    case "Tb_InV_Min":
                    case "Tb_InV_Max":
                    case "Tb_BattV_Min":
                    case "Tb_BattV_Max":
                        MinValue = 0;
                        MaxValue = 999;
                        break;
                    case "Tb_InF_Min":
                    case "Tb_InF_Max":
                    case "Tb_BattLimit_Load":
                        MinValue = 0;
                        MaxValue = 100;
                        break;
                    case "Tb_BattLimit_Time":
                        MinValue = 0;
                        MaxValue = 3600;
                        break;
                    //Min value has to be 1 as 0 can't be assigned to a timer interval (used in Shutdown_Gui)
                    case "Tb_GraceTime":
                    case "Tb_Delay_Stop":
                        MinValue = 1;
                        MaxValue = 3600;
                        break;
                }

                if (control.Text == "")
                {
                    control.Text = MinValue.ToString();
                }

                if (int.TryParse(control.Text, out Result))
                {
                    if (Result >= MinValue && Result <= MaxValue)
                    {
                        LogFile.LogTracing(string.Format("Value of {0} for {1} is valid.", Result, control.Name), LogLvl.LOG_DEBUG, this);
                        control.BackColor = Color.White;
                    }
                    else
                    {
                        LogFile.LogTracing(string.Format("Value of {0} for {1} is invalid.", Result, control.Name), LogLvl.LOG_ERROR, this);
                        e.Cancel = true;
                        control.BackColor = Color.Red;
                    }
                }
                else
                {
                    e.Cancel = true;
                    control.BackColor = Color.Red;
                }
            }
        }

        private void Correct_IP_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            LogFile.LogTracing("Check that the Nut Host address is valid.", LogLvl.LOG_DEBUG, this);
            var control = (TextBox)sender;
            string Pattern;
            string StrTest = control.Text;
            bool Is_Correct = false;
            //Test IPV4
            Pattern = @"^(25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)$";
            if (Regex.IsMatch(control.Text, Pattern))
            {
                Is_Correct = true;
                LogFile.LogTracing("The Nut Host address is a valid IPV4 address.", LogLvl.LOG_WARNING, this);
            }
            //Test IPV6
            Pattern = @"^\s*((([0-9A-Fa-f]{1,4}:){7}([0-9A-Fa-f]{1,4}|:))|(([0-9A-Fa-f]{1,4}:){6}(:[0-9A-Fa-f]{1,4}|((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3})|:))|(([0-9A-Fa-f]{1,4}:){5}(((:[0-9A-Fa-f]{1,4}){1,2})|:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3})|:))|(([0-9A-Fa-f]{1,4}:){4}(((:[0-9A-Fa-f]{1,4}){1,3})|((:[0-9A-Fa-f]{1,4})?:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){3}(((:[0-9A-Fa-f]{1,4}){1,4})|((:[0-9A-Fa-f]{1,4}){0,2}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){2}(((:[0-9A-Fa-f]{1,4}){1,5})|((:[0-9A-Fa-f]{1,4}){0,3}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){1}(((:[0-9A-Fa-f]{1,4}){1,6})|((:[0-9A-Fa-f]{1,4}){0,4}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(:(((:[0-9A-Fa-f]{1,4}){1,7})|((:[0-9A-Fa-f]{1,4}){0,5}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:)))(%.+)?\s*$";
            if (Regex.IsMatch(control.Text, Pattern) && !Is_Correct)
            {
                Is_Correct = true;
                LogFile.LogTracing("The Nut Host address is a valid IPV6 address.", LogLvl.LOG_WARNING, this);
            }
            //Test fqdn
            Pattern = @"^(?:(?!\d+\.|-)[a-zA-Z0-9_\-]{1,63}(?<!-)\.?)+(?:[a-zA-Z]{2,})$";
            if (Regex.IsMatch(control.Text, Pattern) && !Is_Correct)
            {
                Is_Correct = true;
                LogFile.LogTracing("The Nut Host address is a valid FQDN address.", LogLvl.LOG_WARNING, this);
            }

            //Result
            if (Is_Correct)
            {
                control.BackColor = Color.White;
            }
            else
            {
                LogFile.LogTracing("The Nut Host address is a invalid", LogLvl.LOG_ERROR, this);
                e.Cancel = true;
                control.BackColor = Color.Red;
            }
        }

        private void Pref_Gui_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = false;
        }

        private void TabControl_Options_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (TabControl_Options.SelectedTab == Tab_Miscellanous)
            {
                SetLogControlsStatus();
            }
        }

        private void Pref_Gui_Load(object sender, EventArgs e)
        {
            // Restyle on top of the designer layout (see NutDesk.Theming.Theme).
            Theming.Theme.Apply(this);
            Icon = MainForm.Instance.Icon;
            LogFile.LogTracing("Load Pref Gui", LogLvl.LOG_DEBUG, this);
        }

        /// <summary>
        /// Handle any value in the form changing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Event_Ctrl_Value_Changed(object sender, EventArgs e)
        {
            if (IsShowed)
            {
                PrefsModified = true;
                Btn_Apply.Enabled = true;
            }
        }

        #region Logging controls

        private void Btn_ViewLog_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("User clicked ViewLog button.", LogLvl.LOG_DEBUG, this);
            try
            {
                Process.Start(LogFile.LogFilePath);
                LogFile.LogTracing("Opened UI window to log location.", LogLvl.LOG_NOTICE, this);
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, this);
                SetLogControlsStatus();
            }
        }

        private void Btn_DeleteLog_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("User clicked DeleteLog button.", LogLvl.LOG_DEBUG, this);

            try
            {
                LogFile.DeleteLogFile();
                PrefsModified = true; // Will help reinitialize log file later if user still wants it.
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, this);
            }

            SetLogControlsStatus();
        }

        /// <summary>
        /// Enable or disable controls to view and delete log data if it's available.
        /// </summary>
        private void SetLogControlsStatus()
        {
            if (LogFile.IsWritingToFile)
            {
                Btn_ViewLog.Enabled = true;
                Btn_DeleteLog.Enabled = true;
            }
            else
            {
                Btn_ViewLog.Enabled = false;
                Btn_DeleteLog.Enabled = false;
            }
        }

        #endregion
    }
}
