// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using NutDesk.Common;
using OldParams = NutDesk.Common.OldParams;
using static NutDesk.Common.AppGlobals;

namespace NutDesk.Models
{
    internal class UpgradePrefsDialogModel : INotifyPropertyChanged
    {
        private readonly BackgroundWorker upgradeWorker = new BackgroundWorker
        {
            WorkerReportsProgress = true,
            WorkerSupportsCancellation = true
        };

        private OldParams.UpgradableParams oldPrefs;
        private readonly Form _parentForm;

        #region Properties and backing fields

        private bool _ImportPreviousSettings;
        private bool _BackupPreviousSettings;
        private bool _DeletePreviousSettings;
        private bool _OKButtonEnabled;
        private bool _FormEnabled;
        private bool _FormUseWaitCursor;
        private int _ProgressPercent = 0;
        private System.Drawing.Icon _Icon;

        public bool ImportPreviousSettigns
        {
            get => _ImportPreviousSettings;
            set
            {
                _ImportPreviousSettings = value;
                NotifyPropertyChanged();
                // CalculateOKButtonState()
            }
        }

        public bool BackupPreviousSettings
        {
            get => _BackupPreviousSettings;
            set
            {
                if (_BackupPreviousSettings != value)
                {
                    _BackupPreviousSettings = value;
                    NotifyPropertyChanged();
                    // CalculateOKButtonState()
                }
            }
        }

        public bool DeletePreviousSettings
        {
            get => _DeletePreviousSettings;
            set
            {
                _DeletePreviousSettings = value;
                NotifyPropertyChanged();
                // CalculateOKButtonState()
            }
        }

        public bool OKButtonEnabled
        {
            get => _OKButtonEnabled;
            set
            {
                if (_OKButtonEnabled != value)
                {
                    _OKButtonEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Enable/disable the main form and cascade changes down as necessary.
        /// </summary>
        /// <returns></returns>
        public bool FormEnabled
        {
            get => _FormEnabled;
            set
            {
                if (_FormEnabled != value)
                {
                    _FormEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool FormUseWaitCursor
        {
            get => _FormUseWaitCursor;
            set
            {
                if (_FormUseWaitCursor != value)
                {
                    _FormUseWaitCursor = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int ProgressPercent
        {
            get => _ProgressPercent;
            set
            {
                if (value < 0 || value > 100)
                {
                    throw new ArgumentOutOfRangeException(
                            "Progress must be reported as a percentage inbetween 0 and 100.");
                }
                else
                {
                    _ProgressPercent = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public System.Drawing.Icon Icon
        {
            get => _Icon;
            private set
            {
                if (_Icon != null && !_Icon.Equals(value))
                {
                    _Icon = value;
                    NotifyPropertyChanged();
                }
            }
        }

        #endregion

        #region INotifyPropertyChanged implementation

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        internal UpgradePrefsDialogModel(Form parentForm)
        {
            _parentForm = parentForm;

            // VB attached this with "Handles Me.PropertyChanged" on CalculateOKButtonState.
            PropertyChanged += CalculateOKButtonState;

            upgradeWorker.DoWork += ProcessUpgradeWork;
            upgradeWorker.ProgressChanged += UpgradeProgressChanged;
            upgradeWorker.RunWorkerCompleted += UpgradeWorkComplete;
        }

        internal void InitializeProperties()
        {
            FormEnabled = true;
            FormUseWaitCursor = false;
            ImportPreviousSettigns = true;
            BackupPreviousSettings = true;
            Icon = (System.Drawing.Icon)Properties.Resources.AppIcon;
        }

        internal void BeginUpgradeWorkAsync()
        {
            var workerArgs = new UpgradeWorkerArguments();

            if (BackupPreviousSettings)
            {
                // Capture user's backup location preference on this thread before starting background worker.
                var saveFileDialog = new SaveFileDialog
                {
                    FileName = "WinNUT-Prefs-Export",
                    // The file type names sit in the same dialog as the localised title, so they follow the UI culture too.
                    Filter = Properties.Resources.FileFilter_RegistryFiles,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Title = Properties.Resources.UpgradePrefsDialog_BackupLocationTitle
                };

                var dialogRes = saveFileDialog.ShowDialog();

                if (dialogRes == DialogResult.Cancel)
                {
                    LogFile.LogTracing("User cancelled upgrade prefs process while selecting backup target.",
                                       LogLvl.LOG_NOTICE, this);
                    return;
                }

                workerArgs.BackupPath = saveFileDialog.FileName;
            }

            FormEnabled = false;
            FormUseWaitCursor = true;
            upgradeWorker.RunWorkerAsync(workerArgs);
        }

        internal void CancelButtonClicked()
        {
            LogFile.LogTracing("Handling Cancel button click...", LogLvl.LOG_DEBUG, this);

            if (upgradeWorker != null && upgradeWorker.IsBusy)
            {
                LogFile.LogTracing("Requesting cancellation of upgradeWorker.", LogLvl.LOG_NOTICE, this);
                upgradeWorker.CancelAsync();
            }
            else
            {
                LogFile.LogTracing("Exiting out of upgrade dialog.", LogLvl.LOG_NOTICE, this, Properties.Resources.UpgradePrefsDialog_Cancelled);
                _parentForm.DialogResult = DialogResult.Cancel;
                _parentForm.Close();
            }
        }

        private void CalculateOKButtonState(object sender = null, PropertyChangedEventArgs args = null)
        {
            if (FormEnabled && ImportPreviousSettigns || BackupPreviousSettings || DeletePreviousSettings)
            {
                OKButtonEnabled = true;
            }
            else
            {
                OKButtonEnabled = false;
            }
        }

        #region BackgroundWorker

        /// <summary>
        /// Perform the work of <see cref="upgradeWorker"/>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProcessUpgradeWork(object sender, DoWorkEventArgs e)
        {
            var args = (UpgradeWorkerArguments)e.Argument;

            if (!e.Cancel && ImportPreviousSettigns)
            {
                DoImportWork();
            }

            if (!e.Cancel && BackupPreviousSettings)
            {
                DoBackupWork(args.BackupPath);
            }

            if (!e.Cancel && DeletePreviousSettings)
            {
                DoDeleteWork();
            }
        }

        /// <summary>
        /// Execution of <see cref="ProcessUpgradeWork(object, DoWorkEventArgs)"/> has ended and returned to the
        /// UI thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpgradeWorkComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            oldPrefs = null;

            FormEnabled = true;
            FormUseWaitCursor = false;

            if (e.Error != null)
            {
                ProgressPercent = 0;
                var localError = string.Format(Properties.Resources.UpgradePrefsDialog_ErrorEncountered, e.Error.Message);
                LogFile.LogTracing("UpgradeWorkComplete with error: " + Environment.NewLine + e.Error.ToString(),
                                   LogLvl.LOG_ERROR, this, localError);
                MessageBox.Show(localError);
                return;
            }

            if (e.Cancelled)
            {
                ProgressPercent = 0;
                LogFile.LogTracing("Upgrade work was cancelled.", LogLvl.LOG_WARNING, this);
                return;
            }

            ProgressPercent = 100;
            _parentForm.Close();
        }

        /// <summary>
        /// Reports progress from within the BackgroundWorker thread, back to the calling thread.
        /// WARNING: This is not a reliably quick way to synchronize information back to the calling thread.
        /// From testing, the <see cref="ProcessUpgradeWork"/> routine moves much faster than the main thread.
        /// </summary>
        /// <param name="percentComplete"></param>
        /// <param name="logMsg"></param>
        /// <param name="logLevel"></param>
        /// <param name="sender"></param>
        /// <param name="logRes"></param>
        private void ReportProgress(int percentComplete, string logMsg, LogLvl logLevel, object sender,
                                    string logRes = null)
        {
            upgradeWorker.ReportProgress(percentComplete,
                                     new UpgradeWorkerProgressReport(logMsg, logLevel, sender, logRes));
        }

        private void UpgradeProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var progReport = (UpgradeWorkerProgressReport)e.UserState;
            LogFile.LogTracing(progReport.LogOutput, progReport.LogLevel, progReport.Sender, progReport.LogResourceString);
            ProgressPercent = e.ProgressPercentage;
        }

        private void DoImportWork()
        {
            ReportProgress(0, "Import operation beginning.", LogLvl.LOG_NOTICE, this);
            oldPrefs = new OldParams.UpgradableParams();
            ReportProgress(100, "Old parameters loaded.", LogLvl.LOG_NOTICE, this);

            var progress = 0;
            foreach (KeyValuePair<string, Dictionary<string, object>> section in oldPrefs.Parameters)
            {
                foreach (KeyValuePair<string, object> oldPref in section.Value)
                {
                    // VB evaluated this with Double division and narrowed it back to Integer at the
                    // ReportProgress call; Convert.ToInt32 reproduces that rounding.
                    var percentComplete = ((double)progress / oldPrefs.TotalPrefs) * 100;
                    try
                    {
                        var pairLookupRes = oldPrefs.PrefSettingsLookup.First(
                            (OldParams.UpgradableParams.PrefSettingPair p) => p.OldPreferenceName.Equals(oldPref.Key));

                        object oldValue = oldPref.Value;
                        // Handle special case of converting the default input frequency calibration value
                        if (oldPref.Key == "FrequencySupply")
                        {
                            oldValue = (Convert.ToInt32(oldValue) * 10) + 50;
                        }

                        Properties.Settings.Default[pairLookupRes.NewSettingsName] = oldValue;

                        ReportProgress(Convert.ToInt32(percentComplete), "Imported " + oldPref.Key + " into " + pairLookupRes.NewSettingsName,
                            LogLvl.LOG_NOTICE, this);

                        // Remove found entry since we no longer need it.
                        oldPrefs.PrefSettingsLookup.Remove(pairLookupRes);
                        progress += 1;
                    }
                    catch (Exception ex)
                    {
                        ReportProgress(Convert.ToInt32(percentComplete), string.Format("Error importing {0}:{2}{1}",
                        oldPref.Key, Environment.NewLine, ex.ToString()), LogLvl.LOG_ERROR, this);
                    }
                }
            }


            var failedSettingsPairs = oldPrefs.PrefSettingsLookup;

            if (failedSettingsPairs.Count > 0)
            {
                var unmatchedPairsList = string.Empty;
                foreach (var failedPair in failedSettingsPairs)
                {
                    unmatchedPairsList += string.Format("[{0}, {1}]", failedPair.OldPreferenceName, failedPair.NewSettingsName);
                }

                ReportProgress(95, string.Format("{0} unmatched settings pairs: {1}", failedSettingsPairs.Count,
                        unmatchedPairsList), LogLvl.LOG_ERROR, this,
                        string.Format(Properties.Resources.UpgradePrefsDialog_UnmatchedPairs, failedSettingsPairs.Count));
            }

            ReportProgress(100, "Import procedure complete.", LogLvl.LOG_NOTICE, this,
                               Properties.Resources.UpgradePrefsDialog_ImportProcedureCompleted);
        }

        /// <summary>
        /// Performs synchronous work to backup Registry data. Throws exceptions if any error occurs.
        /// </summary>
        /// <param name="targetPath">Full path and filename where registry items will be backed up to.</param>
        private void DoBackupWork(string targetPath)
        {
            ReportProgress(0, "Beginning reg export process to " + targetPath, LogLvl.LOG_NOTICE, this);
            OldParams.NutDesk_Params.ExportParams(targetPath);
            ReportProgress(100, "reg export process exited. Backup complete.", LogLvl.LOG_NOTICE, this,
                string.Format(Properties.Resources.UpgradePrefsDialog_BackupProcedureCompleted, targetPath));
        }

        private void DoDeleteWork()
        {
            ReportProgress(0, "Starting delete procedure.", LogLvl.LOG_NOTICE, this);
            OldParams.NutDesk_Params.DeleteParams();
            ReportProgress(100, "Delete procedure completed successfully.", LogLvl.LOG_NOTICE, this,
                    Properties.Resources.UpgradePrefsDialog_DeleteProcedureComplete);
        }

        #endregion

        protected class UpgradeWorkerProgressReport
        {
            public string LogOutput { get; set; }
            public LogLvl LogLevel { get; set; }
            public object Sender { get; set; }
            public string LogResourceString { get; set; }

            public UpgradeWorkerProgressReport(string logOutput, LogLvl logLevel, object sender, string logRes = null)
            {
                LogOutput = logOutput;
                LogLevel = logLevel;
                Sender = sender;
                LogResourceString = logRes;
            }
        }

        private class UpgradeWorkerArguments
        {
            public string BackupPath { get; set; }
        }
    }
}
