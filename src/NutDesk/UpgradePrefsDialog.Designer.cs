// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

namespace NutDesk.Forms
{
    partial class UpgradePrefsDialog
    {
        // Form overrides dispose to clean up the component list.
        [System.Diagnostics.DebuggerNonUserCode()]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components != null)
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // Required by the Windows Form Designer
        private System.ComponentModel.IContainer components;

        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.
        // Do not modify it using the code editor.
        [System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UpgradePrefsDialog));
            this.PictureBox1 = new System.Windows.Forms.PictureBox();
            this.IntroMessage = new System.Windows.Forms.Label();
            this.OK_Button = new System.Windows.Forms.Button();
            this.UpgradePrefsDialogModelBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.Cancel_Button = new System.Windows.Forms.Button();
            this.ImportSettingsCheckBox = new System.Windows.Forms.CheckBox();
            this.DeleteSettingsCheckBox = new System.Windows.Forms.CheckBox();
            this.PrevSettngsGroupBox = new System.Windows.Forms.GroupBox();
            this.BackupSettingsCheckbox = new System.Windows.Forms.CheckBox();
            this.buttonPanel = new System.Windows.Forms.Panel();
            this.UpgradeProgressBar = new System.Windows.Forms.ProgressBar();
            this.TopContentPanel = new System.Windows.Forms.Panel();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)this.PictureBox1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this.UpgradePrefsDialogModelBindingSource).BeginInit();
            this.PrevSettngsGroupBox.SuspendLayout();
            this.buttonPanel.SuspendLayout();
            this.TopContentPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // PictureBox1
            //
            this.PictureBox1.Image = (System.Drawing.Image)global::NutDesk.Properties.Resources.XP_Information;
            resources.ApplyResources(this.PictureBox1, "PictureBox1");
            this.PictureBox1.Name = "PictureBox1";
            this.PictureBox1.TabStop = false;
            //
            // IntroMessage
            //
            resources.ApplyResources(this.IntroMessage, "IntroMessage");
            this.IntroMessage.Name = "IntroMessage";
            //
            // OK_Button
            //
            resources.ApplyResources(this.OK_Button, "OK_Button");
            this.OK_Button.DataBindings.Add(new System.Windows.Forms.Binding("Enabled", this.UpgradePrefsDialogModelBindingSource, "OKButtonEnabled", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.OK_Button.Name = "OK_Button";
            //
            // UpgradePrefsDialogModelBindingSource
            //
            this.UpgradePrefsDialogModelBindingSource.DataSource = typeof(NutDesk.Models.UpgradePrefsDialogModel);
            //
            // Cancel_Button
            //
            resources.ApplyResources(this.Cancel_Button, "Cancel_Button");
            this.Cancel_Button.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Cancel_Button.Name = "Cancel_Button";
            //
            // ImportSettingsCheckBox
            //
            resources.ApplyResources(this.ImportSettingsCheckBox, "ImportSettingsCheckBox");
            this.ImportSettingsCheckBox.Checked = true;
            this.ImportSettingsCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ImportSettingsCheckBox.DataBindings.Add(new System.Windows.Forms.Binding("Checked", this.UpgradePrefsDialogModelBindingSource, "ImportPreviousSettigns", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.ImportSettingsCheckBox.Name = "ImportSettingsCheckBox";
            this.ToolTip1.SetToolTip(this.ImportSettingsCheckBox, resources.GetString("ImportSettingsCheckBox.ToolTip"));
            this.ImportSettingsCheckBox.UseVisualStyleBackColor = true;
            //
            // DeleteSettingsCheckBox
            //
            resources.ApplyResources(this.DeleteSettingsCheckBox, "DeleteSettingsCheckBox");
            this.DeleteSettingsCheckBox.DataBindings.Add(new System.Windows.Forms.Binding("Checked", this.UpgradePrefsDialogModelBindingSource, "DeletePreviousSettings", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.DeleteSettingsCheckBox.Name = "DeleteSettingsCheckBox";
            this.ToolTip1.SetToolTip(this.DeleteSettingsCheckBox, resources.GetString("DeleteSettingsCheckBox.ToolTip"));
            this.DeleteSettingsCheckBox.UseVisualStyleBackColor = true;
            //
            // PrevSettngsGroupBox
            //
            resources.ApplyResources(this.PrevSettngsGroupBox, "PrevSettngsGroupBox");
            this.PrevSettngsGroupBox.Controls.Add(this.ImportSettingsCheckBox);
            this.PrevSettngsGroupBox.Controls.Add(this.BackupSettingsCheckbox);
            this.PrevSettngsGroupBox.Controls.Add(this.DeleteSettingsCheckBox);
            this.PrevSettngsGroupBox.Name = "PrevSettngsGroupBox";
            this.PrevSettngsGroupBox.TabStop = false;
            //
            // BackupSettingsCheckbox
            //
            resources.ApplyResources(this.BackupSettingsCheckbox, "BackupSettingsCheckbox");
            this.BackupSettingsCheckbox.DataBindings.Add(new System.Windows.Forms.Binding("Checked", this.UpgradePrefsDialogModelBindingSource, "BackupPreviousSettings", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.BackupSettingsCheckbox.Name = "BackupSettingsCheckbox";
            this.ToolTip1.SetToolTip(this.BackupSettingsCheckbox, resources.GetString("BackupSettingsCheckbox.ToolTip"));
            this.BackupSettingsCheckbox.UseVisualStyleBackColor = true;
            //
            // buttonPanel
            //
            this.buttonPanel.Controls.Add(this.Cancel_Button);
            this.buttonPanel.Controls.Add(this.OK_Button);
            resources.ApplyResources(this.buttonPanel, "buttonPanel");
            this.buttonPanel.Name = "buttonPanel";
            //
            // UpgradeProgressBar
            //
            resources.ApplyResources(this.UpgradeProgressBar, "UpgradeProgressBar");
            this.UpgradeProgressBar.DataBindings.Add(new System.Windows.Forms.Binding("Value", this.UpgradePrefsDialogModelBindingSource, "ProgressPercent", true));
            this.UpgradeProgressBar.Name = "UpgradeProgressBar";
            //
            // TopContentPanel
            //
            this.TopContentPanel.Controls.Add(this.PictureBox1);
            this.TopContentPanel.Controls.Add(this.IntroMessage);
            resources.ApplyResources(this.TopContentPanel, "TopContentPanel");
            this.TopContentPanel.Name = "TopContentPanel";
            //
            // UpgradePrefsDialog
            //
            this.AcceptButton = this.OK_Button;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.CancelButton = this.Cancel_Button;
            this.ControlBox = false;
            this.Controls.Add(this.TopContentPanel);
            this.Controls.Add(this.PrevSettngsGroupBox);
            this.Controls.Add(this.buttonPanel);
            this.Controls.Add(this.UpgradeProgressBar);
            this.DataBindings.Add(new System.Windows.Forms.Binding("Enabled", this.UpgradePrefsDialogModelBindingSource, "FormEnabled", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.DataBindings.Add(new System.Windows.Forms.Binding("Icon", this.UpgradePrefsDialogModelBindingSource, "Icon", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "UpgradePrefsDialog";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            ((System.ComponentModel.ISupportInitialize)this.PictureBox1).EndInit();
            ((System.ComponentModel.ISupportInitialize)this.UpgradePrefsDialogModelBindingSource).EndInit();
            this.PrevSettngsGroupBox.ResumeLayout(false);
            this.PrevSettngsGroupBox.PerformLayout();
            this.buttonPanel.ResumeLayout(false);
            this.TopContentPanel.ResumeLayout(false);
            this.TopContentPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        internal System.Windows.Forms.Button OK_Button;
        internal System.Windows.Forms.Button Cancel_Button;
        internal System.Windows.Forms.PictureBox PictureBox1;
        internal System.Windows.Forms.Label IntroMessage;
        internal System.Windows.Forms.CheckBox ImportSettingsCheckBox;
        internal System.Windows.Forms.CheckBox DeleteSettingsCheckBox;
        internal System.Windows.Forms.GroupBox PrevSettngsGroupBox;
        internal System.Windows.Forms.Panel buttonPanel;
        private System.Windows.Forms.ProgressBar UpgradeProgressBar;
        internal System.Windows.Forms.Panel TopContentPanel;
        internal System.Windows.Forms.ToolTip ToolTip1;
        internal System.Windows.Forms.BindingSource UpgradePrefsDialogModelBindingSource;
        internal System.Windows.Forms.CheckBox BackupSettingsCheckbox;
    }
}
