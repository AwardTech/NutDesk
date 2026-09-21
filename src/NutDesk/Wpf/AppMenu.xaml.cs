using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace NutDesk.Wpf
{
    /// <summary>
    /// The application menu behind the dashboard's overflow button.
    /// </summary>
    /// <remarks>
    /// It replaces a WinForms ContextMenuStrip, which could only be opened at the mouse pointer because the
    /// button has no WinForms screen rectangle - so opening it from the keyboard put it wherever the pointer
    /// happened to be. A WPF menu can be anchored to the button itself.
    /// A ContextMenu is the root of its own popup, outside the dashboard's resource scope, and there is no
    /// Application object to fall back on, so the palette is merged into this menu's resources exactly as the
    /// log window does it. Unlike that window it needs no keyboard interop call: the popup is owned by the
    /// dashboard's ElementHost, whose keyboard input already reaches WPF.
    /// </remarks>
    public partial class AppMenu : ContextMenu
    {
        /// <summary>
        /// The transparent margin around the card in AppMenu.xaml, kept so the shadow is not clipped by the popup
        /// window. The two have to agree, or the menu lands that far out of line with the button.
        /// </summary>
        private const double ShadowGutter = 8;

        /// <summary>The space left between the button and the top of the card.</summary>
        private const double ButtonGap = 4;

        private bool paletteIsDark;
        private bool focusFirstItem;

        public AppMenu(bool isDark)
        {
            InitializeComponent();

            paletteIsDark = isDark;
            Resources.MergedDictionaries.Insert(0, Palette.Build(isDark));

            Opened += OnOpened;
        }

        /// <summary>
        /// Follows the shell's theme. The palette always sits at index 0, so this is a single swap rather than
        /// a rebuild, and a menu that happens to be open recolours in place.
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
        /// Opens the menu under <paramref name="anchor"/>, right edges aligned.
        /// </summary>
        /// <param name="anchor">The button the menu belongs to.</param>
        /// <param name="fromKeyboard">
        /// True when the button was activated from the keyboard: the first entry is then selected, as a
        /// keyboard-opened menu is everywhere else in Windows, so the arrow keys have somewhere to start.
        /// </param>
        internal void Open(FrameworkElement anchor, bool fromKeyboard)
        {
            PlacementTarget = anchor;
            Placement = PlacementMode.Bottom;

            // Bottom placement lines up left edges, and the button sits at the right end of the header, so a
            // left-aligned menu would hang past the window. Measuring first gives the width to pull it back by;
            // doing it in Opened instead would show the menu in the wrong place for a frame.
            ApplyTemplate();
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            HorizontalOffset = anchor.ActualWidth - DesiredSize.Width + ShadowGutter;
            VerticalOffset = ButtonGap - ShadowGutter;

            focusFirstItem = fromKeyboard;
            IsOpen = true;
        }

        private void OnOpened(object sender, RoutedEventArgs e)
        {
            // Deferred to input priority so it runs after the menu has finished its own opening work, which
            // records the previously focused element and would otherwise have the last word on focus.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(TakeFocus));
        }

        /// <summary>
        /// Makes sure the menu holds keyboard focus, so arrows, Enter, Escape and the access keys work however
        /// it was opened. Opened from a command rather than by the context-menu service, nothing guarantees it.
        /// </summary>
        private void TakeFocus()
        {
            if (!IsOpen)
            {
                return;
            }

            if (focusFirstItem)
            {
                // Disabled entries and separators cannot take focus, so the first that can is the first usable one.
                var first = Items.Cast<object>()
                                 .Select(item => ItemContainerGenerator.ContainerFromItem(item) as MenuItem)
                                 .FirstOrDefault(container => container != null && container.IsEnabled && container.Focusable);

                if (first != null && first.Focus())
                {
                    return;
                }
            }

            if (!IsKeyboardFocusWithin)
            {
                Focus();
            }
        }
    }
}
