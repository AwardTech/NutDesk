// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.ComponentModel;
using System.Windows.Forms;
using NutDesk.Models;
using NutDesk.Common;
using OldParams = NutDesk.Common.OldParams;

namespace NutDesk.Forms
{
    internal partial class UpgradePrefsDialog : Form
    {
        // VB reached this dialog through its default instance (Forms.UpgradePrefsDialog.ShowDialog());
        // this reproduces the "recreate it if it was closed" behaviour explicitly.
        private static UpgradePrefsDialog _instance;

        internal static UpgradePrefsDialog Instance =>
            (_instance == null || _instance.IsDisposed) ? (_instance = new UpgradePrefsDialog()) : _instance;

        private UpgradePrefsDialogModel backingDataModel;

        public UpgradePrefsDialog()
        {
            InitializeComponent();
            WireEvents();
        }

        /// <summary>
        /// Replaces the VB "Handles" clauses. All event wiring lives here, never in the designer file.
        /// </summary>
        private void WireEvents()
        {
            Load += UpgradePrefsDialog_Load;
            OK_Button.Click += OK_Button_Click;
            Cancel_Button.Click += Cancel_Button_Click;
        }

        private void UpgradePrefsDialog_Load(object sender, EventArgs e)
        {
            // Restyle on top of the designer layout (see NutDesk.Theming.Theme).
            Theming.Theme.Apply(this);
            if (OldParams.NutDesk_Params.RegistryKeyRoot == null)
            {
                MessageBox.Show(Properties.Resources.UpgradePrefsDialog_NoPrefsExistError,
                                Properties.Resources.UpgradePrefsDialog_NoPrefsExistCaption);
                Close();
            }

            // Prepare the backing model for this form, with a reference back to it.
            backingDataModel = new UpgradePrefsDialogModel(this);
            // Connect the BindingSource object to an instance of the data source (our Model class)
            UpgradePrefsDialogModelBindingSource.DataSource = backingDataModel;
            backingDataModel.InitializeProperties();
        }

        private void OK_Button_Click(object sender, EventArgs e)
        {
            backingDataModel.BeginUpgradeWorkAsync();
        }

        private void Cancel_Button_Click(object sender, EventArgs e)
        {
            backingDataModel.CancelButtonClicked();
        }
    }
}
