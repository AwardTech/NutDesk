using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using NutDesk.Common;

namespace NutDesk.Wpf
{
    /// <summary>
    /// The event-log window: the full history with severity, source, a search box and level filters.
    /// </summary>
    /// <remarks>
    /// A WPF Window inside a WinForms application. Two consequences are handled here: there is no
    /// Application object, so the palette has to be merged into this window's own resources exactly as the
    /// dashboard does; and a modeless WPF window shown from a WinForms message loop gets no keyboard input
    /// until <see cref="ElementHost.EnableModelessKeyboardInterop"/> is called for it, which would leave the
    /// search box unusable.
    /// </remarks>
    public partial class LogWindow : Window
    {
        private readonly Logger logger;
        private readonly LogViewModel model;
        private bool paletteIsDark;

        public LogWindow(Logger logger, bool isDark)
        {
            this.logger = logger;

            InitializeComponent();

            paletteIsDark = isDark;
            Resources.MergedDictionaries.Insert(0, Palette.Build(isDark));

            Title = Properties.Resources.LogWindow_Title;

            model = new LogViewModel(logger);
            DataContext = model;

            logger.EventLogged += OnEventLogged;
            ElementHost.EnableModelessKeyboardInterop(this);

            Loaded += (s, e) => SearchBox.Focus();
            Closed += OnClosed;
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

        /// <summary>
        /// Re-reads the state that depends on the preferences rather than on the log itself, so the
        /// "open log file" button does not stay greyed out after file logging is switched on.
        /// </summary>
        internal void RefreshFileState()
        {
            model.RefreshFileState();
        }

        private void OnEventLogged(LogEvent ev)
        {
            // Raised on whichever thread did the logging - the connection worker, most of the time.
            if (!Dispatcher.CheckAccess())
            {
                // BeginInvoke, not Invoke: the logger raises this with no lock held but the caller is often
                // the UI thread itself, and a synchronous hop from a pool thread would serialise logging
                // behind whatever the UI is doing.
                Dispatcher.BeginInvoke(new Action<LogEvent>(OnEventLogged), ev);
                return;
            }

            model.Append(ev);

            if (model.AutoScroll)
            {
                ScrollToNewest();
            }
        }

        /// <summary>
        /// The list is sorted newest first, so following new events means returning to the top.
        /// </summary>
        private void ScrollToNewest()
        {
            var scroller = FindScrollViewer(Rows);
            scroller?.ScrollToTop();
        }

        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer found)
            {
                return found;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var result = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            // Without this the logger keeps a reference to a closed window and every log call marshals to a
            // dispatcher that will never run again.
            logger.EventLogged -= OnEventLogged;
        }
    }
}
