// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.Drawing;
using System.Windows.Forms;
using NutDesk.Common;
using LogLvl = NutDesk.Common.LogLvl;
using AppResxStr = NutDesk.Common.AppResxStr;
using static NutDesk.Common.AppGlobals;

namespace NutDesk
{
    public partial class Shutdown_Gui : Form
    {
        // VB reached this form through its default instance (Shutdown_Gui.Visible = True); this reproduces
        // the "recreate it if it was closed" behaviour explicitly.
        private static Shutdown_Gui _instance;

        internal static Shutdown_Gui Instance =>
            (_instance == null || _instance.IsDisposed) ? (_instance = new Shutdown_Gui()) : _instance;

        private bool RedText = true;
        private readonly WinFormControls.CProgressBar Shutdown_PBar = new WinFormControls.CProgressBar();
        private DateTime Start_Shutdown;
        private double Offset_STimer = 0;
        private double STimer = 0;
        private double Remained = 0;
        public Timer Grace_Timer = new Timer();
        public Timer Shutdown_Timer = new Timer();

        public Shutdown_Gui()
        {
            InitializeComponent();
            WireEvents();
        }

        /// <summary>
        /// Replaces the VB "Handles" clauses. All event wiring lives here, never in the designer file.
        /// </summary>
        private void WireEvents()
        {
            Load += Shutdown_Gui_Load;
            Shown += Shutdown_Gui_Shown;
            FormClosing += Shutdown_Gui_FormClosing;

            Grace_Button.Click += Grace_Button_Click;
            ShutDown_Btn.Click += ShutDown_Btn_Click;
            Run_Timer.Tick += Run_Timer_Tick;
        }

        private void Grace_Button_Click(object sender, EventArgs e)
        {
            Shutdown_Timer.Stop();
            Shutdown_Timer.Enabled = false;
            Grace_Button.Enabled = false;
            Grace_Timer.Enabled = true;
            Grace_Timer.Start();
            Offset_STimer = Properties.Settings.Default.PW_ExtendDelaySec;
        }

        private void Shutdown_Gui_Load(object sender, EventArgs e)
        {
            // Restyle on top of the designer layout (see NutDesk.Theming.Theme).
            Theming.Theme.Apply(this);
            Icon = MainForm.Instance.Icon;
            LogFile.LogTracing("Load ShutDown Gui", LogLvl.LOG_DEBUG, this);
            Grace_Timer.Enabled = false;
            Grace_Timer.Stop();
            //If ExtendedShutdownDelay = 0 (the default value), the next line fails and the whole shutdown sequence fails - Thus no shutdown
            //Moved next line lower down
            //Me.Grace_Timer.Interval = (NutDesk_Params.Arr_Reg_Key.Item("ExtendedShutdownDelay") * 1000)
            Shutdown_Timer.Interval = Properties.Settings.Default.PW_StopDelaySec * 1000;
            STimer = Properties.Settings.Default.PW_StopDelaySec;
            Remained = STimer;
            if (Properties.Settings.Default.PW_UserExtendStopTimer)
            {
                Grace_Button.Enabled = true;
                //Moved here so it is only used if grace period is allowed
                try
                {
                    Grace_Timer.Interval = Properties.Settings.Default.PW_ExtendDelaySec * 1000;
                }
                catch (Exception)
                {
                    //Disable Grace peroid option if Interval is set to 0
                    Grace_Button.Enabled = false;
                }
            }
            else
            {
                Grace_Button.Enabled = false;
            }
            Start_Shutdown = DateTime.Now;

            // VB used a "With Shutdown_PBar" block here.
            Shutdown_PBar.Location = new Point(10, 150);
            // VB wrote "New Point(400, 23)" and relied on the narrowing Point -> Size conversion.
            Shutdown_PBar.Size = new Size(400, 23);
            Shutdown_PBar.Style = ProgressBarStyle.Continuous;
            Shutdown_PBar.Font = new Font(Shutdown_PBar.Font, Shutdown_PBar.Font.Style | FontStyle.Bold);
            Shutdown_PBar.ForeColor = Color.Black;
            string TimeToShow;
            TimeSpan iSpan = TimeSpan.FromSeconds(Shutdown_Timer.Interval / 1000.0);
            if (Shutdown_Timer.Interval == (3600 * 1000))
            {
                TimeToShow = iSpan.Hours.ToString().PadLeft(2, '0') + ":" +
                                        iSpan.Minutes.ToString().PadLeft(2, '0') + ":" +
                                        iSpan.Seconds.ToString().PadLeft(2, '0');
            }
            else
            {
                TimeToShow = iSpan.Minutes.ToString().PadLeft(2, '0') + ":" +
                             iSpan.Seconds.ToString().PadLeft(2, '0');
            }
            Shutdown_PBar.Text = TimeToShow;
            Shutdown_PBar.Value = 0;

            Controls.Add(Shutdown_PBar);
            Grace_Timer.Tick += Grace_Timer_Tick;
            Shutdown_Timer.Tick += Shutdown_Timer_Tick;
        }

        private void Shutdown_Gui_Shown(object sender, EventArgs e)
        {
            Shutdown_Timer.Enabled = true;
            Shutdown_Timer.Start();
            lbl_UPSStatus.Text = string.Format(StrLog[(int)AppResxStr.STR_SHUT_STAT], Convert.ToString(MainForm.Instance.UPS_BattCh), MainForm.Instance.Lbl_VRTime.Text);
            LogFile.LogTracing("Shutdown GUI is shown and timer started for " + Shutdown_Timer.Interval / 1000.0 + " seconds.", LogLvl.LOG_NOTICE, this);
        }

        private void Grace_Timer_Tick(object sender, EventArgs e)
        {
            Shutdown_Timer.Interval = Convert.ToInt32(Remained * 1000);
            Shutdown_Timer.Enabled = true;
            Shutdown_Timer.Start();
        }

        private void ShutDown_Btn_Click(object sender, EventArgs e)
        {
            MainForm.Instance.Shutdown_Action();
        }

        private void Shutdown_Timer_Tick(object sender, EventArgs e)
        {
            LogFile.LogTracing("Shutdown timer tick.", LogLvl.LOG_NOTICE, this);
            Shutdown_PBar.Value = 100;
            System.Threading.Thread.Sleep(1000);
            Run_Timer.Enabled = false;
            Shutdown_Timer.Stop();
            Shutdown_Timer.Enabled = false;
            Grace_Timer.Stop();
            Grace_Timer.Enabled = false;
            Hide();
            MainForm.Instance.Shutdown_Action();

            Close();
        }

        private void Run_Timer_Tick(object sender, EventArgs e)
        {
            if (RedText)
            {
                lbl_UPSStatus.ForeColor = Color.Red;
                RedText = false;
            }
            else
            {
                lbl_UPSStatus.ForeColor = Color.Black;
                RedText = true;
            }
            if (Shutdown_Timer.Enabled == true && Remained > 0)
            {
                // VB's Int() truncates towards negative infinity, i.e. Math.Floor.
                Remained = Math.Floor(STimer + Offset_STimer - DateTime.Now.Subtract(Start_Shutdown).TotalSeconds);
                int NewValue = 100;
                if (Remained > 0)
                {
                    NewValue -= Convert.ToInt32(100 * (Remained / STimer));
                    if (NewValue > 100)
                    {
                        NewValue = 100;
                    }
                }
                string TimeToShow;
                TimeSpan iSpan = TimeSpan.FromSeconds(Remained);
                if (Shutdown_Timer.Interval == (3600 * 1000))
                {
                    TimeToShow = iSpan.Hours.ToString().PadLeft(2, '0') + ":" +
                                            iSpan.Minutes.ToString().PadLeft(2, '0') + ":" +
                                            iSpan.Seconds.ToString().PadLeft(2, '0');
                }
                else
                {
                    TimeToShow = iSpan.Minutes.ToString().PadLeft(2, '0') + ":" +
                                 iSpan.Seconds.ToString().PadLeft(2, '0');
                }
                Shutdown_PBar.Text = TimeToShow;
                Shutdown_PBar.Value = NewValue;
                lbl_UPSStatus.Text = string.Format(StrLog[(int)AppResxStr.STR_SHUT_STAT], Convert.ToString(MainForm.Instance.UPS_BattCh), MainForm.Instance.Lbl_VRTime.Text);
            }
        }

        private void Shutdown_Gui_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Visible)
            {
                e.Cancel = true;
            }
        }
    }
}
