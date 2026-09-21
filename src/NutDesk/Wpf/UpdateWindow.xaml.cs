using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Threading;
using NutDesk.Common.Updater;

namespace NutDesk.Wpf
{
    /// <summary>
    /// The update window: a newer release, its notes, and the way to install it. Replaces the WinForms form WinNUT
    /// had, which only downloaded the archive.
    /// </summary>
    /// <remarks>
    /// A WPF Window inside a WinForms application, handled the way <see cref="LogWindow"/> handles it: there is no
    /// Application object, so the palette is merged into this window's own resources, and a modeless WPF window shown
    /// from a WinForms message loop gets no keyboard input until <see cref="ElementHost.EnableModelessKeyboardInterop"/>
    /// is called for it. The shell keeps at most one open (see MainForm.ShowUpdateWindow).
    /// </remarks>
    public partial class UpdateWindow : Window
    {
        private readonly UpdateViewModel model;
        private bool paletteIsDark;

        /// <param name="update">A check result with an update available.</param>
        /// <param name="isDark">The palette to start with.</param>
        /// <param name="installBlocker">See <see cref="UpdateViewModel"/>: why installing has to wait, or null.</param>
        /// <param name="exitForInstall">See <see cref="UpdateViewModel"/>: exits the application for the new version.</param>
        internal UpdateWindow(UpdateCheckResult update, bool isDark, Func<string> installBlocker, Action exitForInstall)
        {
            InitializeComponent();

            paletteIsDark = isDark;
            Resources.MergedDictionaries.Insert(0, Palette.Build(isDark));

            model = new UpdateViewModel(update, installBlocker, exitForInstall);
            DataContext = model;
            Title = model.WindowTitle;

            model.CloseRequested += Close;
            model.StepChanged += OnStepChanged;
            model.PropertyChanged += OnModelPropertyChanged;
            ElementHost.EnableModelessKeyboardInterop(this);

            KeyDown += OnKeyDown;
            Loaded += OnLoaded;
            Closed += (s, e) => model.OnClosed();
        }

        /// <summary>The release this window offers.</summary>
        internal UpdateCheckResult Update => model.Update;

        /// <summary>
        /// Follows the shell's theme while the window is open. The palette always sits at index 0, so this is a single
        /// swap rather than a rebuild.
        /// </summary>
        internal void ApplyPalette(bool isDark)
        {
            if (isDark == paletteIsDark && Resources.MergedDictionaries.Count > 0)
            {
                return;
            }

            paletteIsDark = isDark;
            Resources.MergedDictionaries[0] = Palette.Build(isDark);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ShowActivated)
            {
                MainButton().Focus();
            }
            else
            {
                // Opened in the background by an automatic check: only the focus the window will have once the user
                // goes to it. Moving the keyboard focus now could pull the window in front of whatever they are typing.
                FocusManager.SetFocusedElement(this, MainButton());
            }
        }

        /// <summary>
        /// The button that matters in the current step: Cancel while working, otherwise the primary action when there
        /// is one, otherwise Close. Read from the model, which is ahead of the bindings that show and hide them.
        /// </summary>
        private System.Windows.Controls.Button MainButton()
        {
            if (model.ShowCancel)
            {
                return CancelButton;
            }

            return model.ShowPrimary ? PrimaryButton : CloseButton;
        }

        private void OnStepChanged()
        {
            // Posted: the bindings that show and hide the buttons are updated in the same pass, and the focus can
            // only go to a button that is already visible.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                // Only when the keyboard focus was on the button that has just been swapped out, or has been lost
                // with it; someone reading the notes during the download keeps their place.
                var focused = Keyboard.FocusedElement;
                if (!IsKeyboardFocusWithin || ReferenceEquals(focused, PrimaryButton) || ReferenceEquals(focused, CancelButton))
                {
                    if (IsActive)
                    {
                        MainButton().Focus();
                    }
                }
            }));
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(UpdateViewModel.StatusText))
            {
                return;
            }

            // The status line is a live region: a screen reader announces the new step without the user having to
            // go and look for it.
            if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    var peer = UIElementAutomationPeer.FromElement(StatusLine) ?? UIElementAutomationPeer.CreatePeerForElement(StatusLine);
                    peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }));
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Escape closes, as the Close button does; a download under way is abandoned with the window.
            if (e.Key == Key.Escape && !e.Handled)
            {
                e.Handled = true;
                Close();
            }
        }
    }
}
