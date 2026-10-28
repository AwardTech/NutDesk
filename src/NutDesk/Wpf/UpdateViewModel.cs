using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using NutDesk.Common;
using NutDesk.Common.Updater;
using NutDesk.Updating;

namespace NutDesk.Wpf
{
    /// <summary>
    /// Where the update window is in its work.
    /// </summary>
    public enum UpdateStep
    {
        /// <summary>Showing the release; nothing started yet, or a download was cancelled.</summary>
        Available,

        /// <summary>Fetching the published checksum and the package.</summary>
        Downloading,

        /// <summary>Comparing the package with its published SHA-256.</summary>
        Verifying,

        /// <summary>Unpacking the package and checking the NutDesk.exe inside.</summary>
        Extracting,

        /// <summary>Staged and verified; waiting for the user to install and restart.</summary>
        Ready,

        /// <summary>The new version has been started to install itself, and NutDesk is exiting.</summary>
        Installing,

        /// <summary>Staged, but this folder cannot be updated in place: the user copies the files over it.</summary>
        Manual,

        /// <summary>The preparation failed; the status says why.</summary>
        Failed,
    }

    /// <summary>How the status line of the update window reads.</summary>
    public enum UpdateStatusTone
    {
        Neutral,
        Success,
        Warning,
        Error,
    }

    /// <summary>
    /// One block of the release notes as the update window draws it.
    /// </summary>
    public sealed class ReleaseNoteLine
    {
        // List items indent by this much per level, which is about the width of a bullet and its gap.
        private const double ListIndent = 18;

        internal ReleaseNoteLine(ReleaseNoteBlock block, bool isFirst)
        {
            Text = block.Text;
            Marker = block.Marker;
            IsHeading = block.Kind == ReleaseNoteKind.Heading && block.Level <= 2;
            IsMinorHeading = block.Kind == ReleaseNoteKind.Heading && block.Level > 2;
            IsCode = block.Kind == ReleaseNoteKind.Code;

            switch (block.Kind)
            {
                case ReleaseNoteKind.Heading:
                    // A heading opens a section, so it keeps its distance from the one above - except at the very top.
                    Margin = new Thickness(0, isFirst ? 0 : 12, 0, 4);
                    break;
                case ReleaseNoteKind.Bullet:
                    Margin = new Thickness(block.Level * ListIndent, 2, 0, 2);
                    break;
                case ReleaseNoteKind.Code:
                    Margin = new Thickness(ListIndent, 4, 0, 4);
                    break;
                default:
                    Margin = new Thickness(0, isFirst ? 0 : 4, 0, 4);
                    break;
            }
        }

        public string Text { get; }

        /// <summary>A list item's bullet or number; null otherwise.</summary>
        public string Marker { get; }

        public bool HasMarker => Marker != null;

        /// <summary>A top-level heading ("#" or "##").</summary>
        public bool IsHeading { get; }

        /// <summary>A heading of level three or deeper.</summary>
        public bool IsMinorHeading { get; }

        public bool IsCode { get; }

        public Thickness Margin { get; }
    }

    /// <summary>
    /// Backs the update window: the release it offers, and the way from "Install update" to a NutDesk that restarts
    /// as the new version - download with progress, SHA-256 verification, extraction, confirmation, install - or, when
    /// this folder cannot be written, to the files the user copies by hand.
    /// </summary>
    /// <remarks>
    /// The work itself is <see cref="UpdateUtil"/>'s; this class only drives it and puts every step into words. It is
    /// created and used on the UI thread: the updater's async methods resume there, and the progress reports arrive
    /// there through the <see cref="Progress{T}"/> created in <see cref="StartPreparation"/>.
    /// Two things belong to the shell and are passed in: whether installing has to wait (a shutdown in progress, the
    /// UPS on battery), and the exit that lets the new version replace this one.
    /// </remarks>
    public sealed class UpdateViewModel : INotifyPropertyChanged
    {
        private readonly UpdateCheckResult update;
        private readonly Func<string> installBlocker;
        private readonly Action exitForInstall;
        private readonly Logger logger = AppGlobals.LogFile;

        private CancellationTokenSource preparation;
        private PreparedUpdate prepared;
        private bool isClosed;

        private UpdateStep step;
        private string statusText;
        private UpdateStatusTone statusTone;
        private string noteText;
        private double progressValue;
        private bool isProgressIndeterminate = true;
        private string progressText;

        /// <param name="update">A check result with <see cref="UpdateCheckOutcome.UpdateAvailable"/>.</param>
        /// <param name="installBlocker">
        /// Asked right before installing: a sentence saying why the install has to wait, or null when it may go ahead.
        /// </param>
        /// <param name="exitForInstall">
        /// Called once the new version has been started: the application has to exit for real now, because the new
        /// version waits for this process to end before it copies itself over the install folder.
        /// </param>
        internal UpdateViewModel(UpdateCheckResult update, Func<string> installBlocker, Action exitForInstall)
        {
            this.update = update ?? throw new ArgumentNullException(nameof(update));
            this.installBlocker = installBlocker;
            this.exitForInstall = exitForInstall;

            Notes = ReleaseNotes.Parse(update.ReleaseNotes)
                                .Select((block, index) => new ReleaseNoteLine(block, index == 0))
                                .ToList();

            PrimaryCommand = new RelayCommand(RunPrimary, () => ShowPrimary);
            CancelCommand = new RelayCommand(CancelPreparation, () => ShowCancel);
            SkipCommand = new RelayCommand(Skip, () => !IsBusy);
            ReleasePageCommand = new RelayCommand(OpenReleasePage, () => ReleasePage != null && step != UpdateStep.Installing);
            CloseCommand = new RelayCommand(RequestClose);

            step = UpdateStep.Available;
            if (!update.HasPackage)
            {
                // Nothing to install in place; the release page is the way forward.
                SetStatus(Properties.Resources.Update_NoAsset, UpdateStatusTone.Neutral);
            }
        }

        /// <summary>The window has to close: Close or Skip was chosen.</summary>
        public event Action CloseRequested;

        /// <summary>
        /// Raised after <see cref="Step"/> changed, so the view can move the keyboard focus off a button that has just
        /// been hidden.
        /// </summary>
        public event Action StepChanged;

        /// <summary>The release this window offers.</summary>
        internal UpdateCheckResult Update => update;

        /// <summary>
        /// The folder to update: the one this copy runs from. System.Windows.Forms spelled out, because in a WPF file
        /// "Application" would be the WPF class.
        /// </summary>
        private static string InstallDirectory => System.Windows.Forms.Application.StartupPath;

        #region Release

        public string WindowTitle => Properties.Resources.UpdateWindow_Title;

        /// <summary>"NutDesk 1.2.0 is available".</summary>
        public string Heading => UpdateText.Format(Properties.Resources.UpdateWindow_Heading, update.VersionText);

        /// <summary>"You have version 1.0.0 · Released on ...".</summary>
        public string Subheading
        {
            get
            {
                var installed = UpdateText.Format(Properties.Resources.UpdateWindow_Installed, UpdateUtil.RunningVersionText);
                var published = update.PublishedAt;
                if (published == null)
                {
                    return installed;
                }

                var date = published.Value.LocalDateTime.ToString("D", CultureInfo.CurrentCulture);
                return installed + "  ·  " + UpdateText.Format(Properties.Resources.UpdateWindow_Released, date);
            }
        }

        public bool IsPreRelease => update.Release?.Prerelease ?? false;

        public string PreReleaseCaption => Properties.Resources.UpdateWindow_PreRelease;

        public string NotesHeading => Properties.Resources.UpdateWindow_WhatsNew;

        public IList<ReleaseNoteLine> Notes { get; }

        public bool HasNoNotes => Notes.Count == 0;

        public string NoNotesText => Properties.Resources.UpdateWindow_NoNotes;

        #endregion

        #region State

        public UpdateStep Step => step;

        /// <summary>Downloading, verifying, extracting or installing: the window is working and most buttons wait.</summary>
        public bool IsBusy => IsPreparing || step == UpdateStep.Installing;

        private bool IsPreparing =>
            step == UpdateStep.Downloading || step == UpdateStep.Verifying || step == UpdateStep.Extracting;

        /// <summary>The main line of the status panel: what is happening, or what went wrong.</summary>
        public string StatusText => statusText;

        public UpdateStatusTone StatusTone => statusTone;

        /// <summary>The status panel's second line, in plain colour: what the user can do next.</summary>
        public string NoteText => noteText;

        public bool HasNote => !string.IsNullOrEmpty(noteText);

        public bool HasStatus => !string.IsNullOrEmpty(statusText) || HasNote || ShowProgress;

        public bool ShowProgress => IsPreparing;

        /// <summary>0 to 100.</summary>
        public double ProgressValue => progressValue;

        /// <summary>True while nothing measurable is going on: verifying, extracting, or a download of unknown size.</summary>
        public bool IsProgressIndeterminate => isProgressIndeterminate;

        /// <summary>"1.2 MB of 5.0 MB" while downloading.</summary>
        public string ProgressText => progressText;

        #endregion

        #region Buttons

        public string PrimaryCaption
        {
            get
            {
                switch (step)
                {
                    case UpdateStep.Ready:
                        return Properties.Resources.UpdateWindow_InstallNow;
                    case UpdateStep.Manual:
                        return Properties.Resources.UpdateWindow_OpenFolder;
                    case UpdateStep.Failed:
                        return Properties.Resources.UpdateWindow_Retry;
                    default:
                        return Properties.Resources.UpdateWindow_Install;
                }
            }
        }

        public string PrimaryTip
        {
            get
            {
                switch (step)
                {
                    case UpdateStep.Available:
                    case UpdateStep.Failed:
                        return Properties.Resources.UpdateWindow_InstallTip;
                    case UpdateStep.Manual:
                        return Properties.Resources.UpdateWindow_OpenFolderTip;
                    default:
                        return null;
                }
            }
        }

        /// <summary>
        /// Install update, Try again, Install and restart or Open folder, depending on the step. Hidden while working,
        /// and for a release with nothing to install.
        /// </summary>
        public bool ShowPrimary
        {
            get
            {
                switch (step)
                {
                    case UpdateStep.Available:
                    case UpdateStep.Failed:
                        return update.HasPackage;
                    case UpdateStep.Ready:
                        return prepared != null;
                    case UpdateStep.Manual:
                        return prepared != null;
                    default:
                        return false;
                }
            }
        }

        /// <summary>Only while the update is being prepared; it takes the place of the primary button.</summary>
        public bool ShowCancel => IsPreparing;

        public string CancelCaption => Properties.Resources.UpdateWindow_Cancel;

        public string SkipCaption => Properties.Resources.UpdateWindow_Skip;

        public string SkipTip => Properties.Resources.UpdateWindow_SkipTip;

        public string ReleasePageCaption => Properties.Resources.UpdateWindow_ReleasePage;

        public string ReleasePageTip => Properties.Resources.Update_OpenReleasePage;

        public string CloseCaption => Properties.Resources.UpdateWindow_Close;

        public RelayCommand PrimaryCommand { get; }

        public RelayCommand CancelCommand { get; }

        public RelayCommand SkipCommand { get; }

        public RelayCommand ReleasePageCommand { get; }

        public RelayCommand CloseCommand { get; }

        #endregion

        #region Actions

        private void RunPrimary()
        {
            switch (step)
            {
                case UpdateStep.Available:
                case UpdateStep.Failed:
                    StartPreparation();
                    break;
                case UpdateStep.Ready:
                    InstallNow();
                    break;
                case UpdateStep.Manual:
                    OpenStagedFolder();
                    break;
            }
        }

        /// <summary>
        /// Download, verify, extract. Never throws: every failure ends in <see cref="UpdateStep.Failed"/> with its
        /// reason in words, and UpdateUtil has logged the technical detail.
        /// </summary>
        private async void StartPreparation()
        {
            if (IsBusy || !update.HasPackage)
            {
                return;
            }

            prepared = null;
            preparation = new CancellationTokenSource();
            SetDownloadProgress(0, update.Package.Size);
            SetStatus(Properties.Resources.UpdateWindow_Downloading, UpdateStatusTone.Neutral);
            SetNote(null);
            SetStep(UpdateStep.Downloading);

            var progress = new Progress<UpdateProgress>(OnProgress);
            PreparedUpdate result;
            try
            {
                result = await AppGlobals.UpdateController.PrepareUpdateAsync(update, progress, preparation.Token);
            }
            catch (OperationCanceledException)
            {
                if (!isClosed)
                {
                    SetStatus(Properties.Resources.UpdateWindow_Cancelled, UpdateStatusTone.Neutral);
                    SetStep(UpdateStep.Available);
                }

                return;
            }
            catch (Exception ex)
            {
                if (!isClosed)
                {
                    SetStatus(UpdateText.Describe(ex), UpdateStatusTone.Error);
                    SetStep(UpdateStep.Failed);
                }

                return;
            }
            finally
            {
                preparation.Dispose();
                preparation = null;
            }

            if (isClosed)
            {
                return;
            }

            prepared = result;
            OfferInstall();
        }

        private void OnProgress(UpdateProgress report)
        {
            // A report can still be queued when the preparation has already finished, or been abandoned.
            if (isClosed || !IsPreparing || report == null)
            {
                return;
            }

            switch (report.Stage)
            {
                case UpdateStage.Downloading:
                    SetDownloadProgress(report.BytesReceived, report.TotalBytes);
                    break;
                case UpdateStage.Verifying:
                    SetIndeterminate();
                    SetStatus(Properties.Resources.UpdateWindow_Verifying, UpdateStatusTone.Neutral);
                    SetStep(UpdateStep.Verifying);
                    break;
                case UpdateStage.Extracting:
                    SetIndeterminate();
                    SetStatus(Properties.Resources.UpdateWindow_Extracting, UpdateStatusTone.Neutral);
                    SetStep(UpdateStep.Extracting);
                    break;
            }
        }

        /// <summary>
        /// The update is staged. In place when this folder can be written; otherwise the files go to the user.
        /// </summary>
        private void OfferInstall()
        {
            if (!UpdateInstaller.CanInstallInPlace(InstallDirectory))
            {
                EnterManual(null);
                return;
            }

            var confirm = UpdateText.Format(Properties.Resources.Update_ConfirmInstall, update.VersionText);
            var blocker = installBlocker?.Invoke();
            if (blocker == null)
            {
                SetStatus(Properties.Resources.UpdateWindow_Verified, UpdateStatusTone.Success);
            }
            else
            {
                SetStatus(blocker, UpdateStatusTone.Warning);
            }

            SetNote(confirm);
            SetStep(UpdateStep.Ready);
        }

        /// <summary>
        /// "Install and restart": the conditions are checked again - the UPS may have gone on battery since - and the
        /// new version is started to replace this one, which then exits.
        /// </summary>
        private void InstallNow()
        {
            if (step != UpdateStep.Ready || prepared == null)
            {
                return;
            }

            var blocker = installBlocker?.Invoke();
            if (blocker != null)
            {
                logger.LogTracing("Update installation postponed: " + blocker, LogLvl.LOG_NOTICE, this);
                SetStatus(blocker, UpdateStatusTone.Warning);
                return;
            }

            if (!UpdateInstaller.CanInstallInPlace(InstallDirectory))
            {
                EnterManual(null);
                return;
            }

            try
            {
                AppGlobals.UpdateController.StartInstall(prepared, InstallDirectory, AppGlobals.PersistDataInStartupPathRequested);
            }
            catch (Exception ex)
            {
                // The new version could not be started; this one keeps running, and the files are there to copy.
                logger.LogTracing("The in-place update could not be started; offering the manual update.", LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
                EnterManual(UpdateText.Describe(ex));
                return;
            }

            SetStatus(Properties.Resources.UpdateWindow_Installing, UpdateStatusTone.Neutral);
            SetNote(null);
            SetStep(UpdateStep.Installing);

            exitForInstall?.Invoke();
        }

        /// <summary>
        /// The manual update: the staged folder opens in Explorer and the window says what to copy where.
        /// </summary>
        private void EnterManual(string reason)
        {
            if (reason == null)
            {
                SetStatus(Properties.Resources.UpdateWindow_Verified, UpdateStatusTone.Success);
            }
            else
            {
                SetStatus(reason, UpdateStatusTone.Error);
            }

            SetNote(UpdateText.Format(Properties.Resources.Update_ManualInstall, InstallDirectory));
            SetStep(UpdateStep.Manual);

            logger.LogTracing("Manual update offered: the new version is in " + prepared?.PayloadDirectory + ".", LogLvl.LOG_NOTICE, this);
            OpenStagedFolder();
        }

        private void OpenStagedFolder()
        {
            var folder = prepared?.PayloadDirectory;
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            try
            {
                Process.Start("explorer.exe", UpdateCommandLine.Quote(folder));
            }
            catch (Exception ex)
            {
                logger.LogTracing("Could not show the staged update in Explorer.", LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
            }
        }

        private void CancelPreparation()
        {
            if (preparation != null && !preparation.IsCancellationRequested)
            {
                logger.LogTracing("Update download cancelled by the user.", LogLvl.LOG_DEBUG, this);
                preparation.Cancel();
            }
        }

        /// <summary>
        /// Automatic checks stay quiet about this release from now on; a check the user asks for still shows it.
        /// </summary>
        private void Skip()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                Properties.Settings.Default.UP_SkippedVersion = update.VersionKey;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                logger.LogTracing("Unable to save the skipped version.", LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
            }

            logger.LogTracing("Update " + update.VersionKey + " skipped by the user.", LogLvl.LOG_NOTICE, this,
                              UpdateText.Format(Properties.Resources.UpdateWindow_Skipped, update.VersionText));
            RequestClose();
        }

        /// <summary>
        /// The release's page, when GitHub gave one that is a web address. The link comes from the network, and
        /// handing anything else to the shell could start a program.
        /// </summary>
        private Uri ReleasePage
        {
            get
            {
                return Uri.TryCreate(update.ReleasePageUrl, UriKind.Absolute, out Uri uri) &&
                       (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                    ? uri
                    : null;
            }
        }

        private void OpenReleasePage()
        {
            var page = ReleasePage;
            if (page == null)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(page.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                logger.LogTracing("Could not open the release page.", LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
            }
        }

        private void RequestClose()
        {
            CloseRequested?.Invoke();
        }

        /// <summary>
        /// The window has closed, whichever way. A download still running is abandoned; the updater removes its
        /// partial file.
        /// </summary>
        internal void OnClosed()
        {
            isClosed = true;
            CancelPreparation();
        }

        #endregion

        #region Helpers

        private void SetStep(UpdateStep value)
        {
            if (step == value)
            {
                return;
            }

            step = value;
            Raise(nameof(Step));
            Raise(nameof(IsBusy));
            Raise(nameof(ShowPrimary));
            Raise(nameof(PrimaryCaption));
            Raise(nameof(PrimaryTip));
            Raise(nameof(ShowCancel));
            Raise(nameof(ShowProgress));
            Raise(nameof(HasStatus));

            // The command manager re-queries on input, not on a change made by code at the end of a download.
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            StepChanged?.Invoke();
        }

        private void SetStatus(string text, UpdateStatusTone tone)
        {
            statusTone = tone;
            statusText = text;
            Raise(nameof(StatusTone));
            Raise(nameof(StatusText));
            Raise(nameof(HasStatus));
        }

        private void SetNote(string text)
        {
            noteText = text;
            Raise(nameof(NoteText));
            Raise(nameof(HasNote));
            Raise(nameof(HasStatus));
        }

        private void SetDownloadProgress(long received, long total)
        {
            if (total > 0)
            {
                progressValue = Math.Max(0, Math.Min(100, received * 100.0 / total));
                isProgressIndeterminate = false;
                progressText = UpdateText.Format(Properties.Resources.UpdateWindow_Progress, FormatSize(received), FormatSize(total));
            }
            else
            {
                progressValue = 0;
                isProgressIndeterminate = true;
                progressText = FormatSize(received);
            }

            Raise(nameof(ProgressValue));
            Raise(nameof(IsProgressIndeterminate));
            Raise(nameof(ProgressText));
        }

        private void SetIndeterminate()
        {
            isProgressIndeterminate = true;
            progressText = null;
            Raise(nameof(IsProgressIndeterminate));
            Raise(nameof(ProgressText));
        }

        private static string FormatSize(long bytes)
        {
            return (bytes / 1048576.0).ToString("0.0", CultureInfo.CurrentCulture) + " MB";
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
