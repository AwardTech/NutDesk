using System;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Input;

namespace NutDesk.Wpf
{
    /// <summary>
    /// The preferences window: the settings Pref_Gui edited, as a navigation rail and cards instead of tabs.
    /// </summary>
    /// <remarks>
    /// A WPF Window inside a WinForms application, handled the way <see cref="LogWindow"/> handles it: there is
    /// no Application object, so the palette is merged into this window's own resources; and a WPF window shown
    /// from a WinForms message loop gets no keyboard input until
    /// <see cref="ElementHost.EnableModelessKeyboardInterop"/> is called for it. A modal ShowDialog runs its own
    /// loop and would not need it, but calling it costs nothing and keeps the window usable whichever way the
    /// shell chooses to open it.
    /// Pref_Gui stays in the project even though nothing opens it: its resources hold the seven translations
    /// this window reads (see <see cref="PrefStrings"/>).
    /// </remarks>
    public partial class PreferencesWindow : Window
    {
        private readonly PreferencesViewModel model;
        private bool paletteIsDark;

        /// <summary>
        /// Raised after every successful save. Static, like Pref_Gui's: the shell subscribes once at startup
        /// and keeps receiving it across every window it opens afterwards.
        /// </summary>
        public static event Action SavedPreferences;

        public PreferencesWindow(bool isDark)
        {
            InitializeComponent();

            paletteIsDark = isDark;
            Resources.MergedDictionaries.Insert(0, Palette.Build(isDark));

            Title = PrefStrings.Title;

            model = new PreferencesViewModel(RaiseSavedPreferences);
            DataContext = model;

            // PasswordBox.Password is deliberately not a dependency property, so it cannot be bound: the stored
            // value goes in by hand before the handler is attached, and every edit comes back the same way.
            PasswordInput.Password = model.Password;
            PasswordInput.PasswordChanged += (s, e) => model.Password = PasswordInput.Password;
            SmtpPasswordInput.Password = model.SmtpPassword;
            SmtpPasswordInput.PasswordChanged += (s, e) => model.SmtpPassword = SmtpPasswordInput.Password;

            model.CloseRequested += Close;
            ElementHost.EnableModelessKeyboardInterop(this);

            KeyDown += OnKeyDown;

            if (model.LoadFailed)
            {
                // Pref_Gui closed itself when it could not show the stored settings. A window cannot be closed
                // before it has been shown, so this waits until it is.
                Loaded += (s, e) => Close();
            }
        }

        /// <summary>
        /// Follows the shell's theme while the window is open. The palette always sits at index 0, so this
        /// is a single swap rather than a rebuild.
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

        private static void RaiseSavedPreferences()
        {
            SavedPreferences?.Invoke();
        }

        /// <summary>
        /// Stands in for the WinForms Validating event's one side effect worth keeping: a box left empty gets
        /// its minimum, and the polling interval snaps back into range, once the user moves on.
        /// </summary>
        private void OnValidatedBoxLostFocus(object sender, RoutedEventArgs e)
        {
            var field = (sender as FrameworkElement)?.DataContext as InputField;
            field?.Normalize();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Bubbling rather than Preview, so an open combo box gets to close its drop-down on Escape first and
            // marks the key handled; only an Escape nothing else wanted discards the window.
            if (e.Key == Key.Escape && !e.Handled)
            {
                e.Handled = true;
                model.Cancel();
            }
        }
    }
}
