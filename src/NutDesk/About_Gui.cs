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
using System.Windows.Forms;
using NutDesk.Common;
using static NutDesk.Common.AppGlobals;

namespace NutDesk
{
    public partial class About_Gui : Form
    {
        // VB reached this form through its default instance (About_Gui.Visible = True); this reproduces
        // the "recreate it if it was closed" behaviour explicitly.
        private static About_Gui _instance;

        internal static About_Gui Instance =>
            (_instance == null || _instance.IsDisposed) ? (_instance = new About_Gui()) : _instance;

        public About_Gui()
        {
            InitializeComponent();
            WireEvents();
        }

        /// <summary>
        /// Replaces the VB "Handles" clauses. All event wiring lives here, never in the designer file.
        /// </summary>
        private void WireEvents()
        {
            Load += About_Gui_Load;
            Btn_OK.Click += Btn_OK_Click;
            LkLbl_Github.LinkClicked += LkLbl_Github_LinkClicked;
        }

        private void About_Gui_Load(object sender, EventArgs e)
        {
            // Restyle on top of the designer layout (see NutDesk.Theming.Theme).
            Theming.Theme.Apply(this);
            Lbl_ProgNameVersion.Text = ProgramName + Environment.NewLine + "Version " + ProgramVersion;
            Lbl_Copyright_2019.Text = Copyright.Replace("©", Environment.NewLine + "©");
            LkLbl_Github.Text = GitHubURL;
            Icon = MainForm.Instance.Icon;
            // Me.LogFile = MainForm.LogFile
            LogFile.LogTracing("Load About Gui", LogLvl.LOG_DEBUG, this);
        }

        private void Btn_OK_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Close About Gui", LogLvl.LOG_DEBUG, this);
            Close();
        }

        private void LkLbl_Github_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(((LinkLabel)sender).Text);
        }
    }
}
