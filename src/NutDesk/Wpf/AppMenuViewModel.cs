using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
// Not "Forms": inside NutDesk.Wpf that name finds the project's own NutDesk.Forms namespace
// before it ever reaches a using alias.
using WinForms = System.Windows.Forms;

namespace NutDesk.Wpf
{
    /// <summary>
    /// The application menu as the WPF view sees it: a live mirror of the shell's WinForms menu.
    /// </summary>
    /// <remarks>
    /// The ToolStripMenuItems stay the model. They carry the translations for all seven cultures through the
    /// form's .resx files, their Click handlers, and the Enabled and Checked state the shell already keeps
    /// current; the shell also adds entries to them at runtime (Theme, the event log, the DEBUG menu).
    /// Re-describing all of that in XAML would mean keeping two menus in step by hand, so this only reads the
    /// items and clicks them.
    /// </remarks>
    public class AppMenuViewModel
    {
        private readonly WinForms.ToolStripItemCollection source;

        public AppMenuViewModel(WinForms.ToolStripItemCollection source)
        {
            this.source = source;
            Rebuild();
        }

        public ObservableCollection<AppMenuItemViewModel> Items { get; } = new ObservableCollection<AppMenuItemViewModel>();

        /// <summary>
        /// Re-reads the whole tree. Called each time the menu opens, because entries are added to the WinForms
        /// menu after the dashboard exists - the DEBUG menu is inserted by a Load handler that runs later.
        /// </summary>
        public void Rebuild()
        {
            Items.Clear();

            foreach (var item in Mirror(source))
            {
                Items.Add(item);
            }
        }

        /// <summary>
        /// Re-reads captions and state without replacing the items, so a menu that is open while the
        /// connection comes up or drops does not go on offering a stale Connect or Disconnect.
        /// </summary>
        public void Refresh()
        {
            foreach (var item in Items)
            {
                item.Refresh();
            }
        }

        internal static AppMenuItemViewModel[] Mirror(WinForms.ToolStripItemCollection items)
        {
            // Available, not Visible: Visible also asks whether the parent strip is shown, and Main_Menu never is.
            return items.Cast<WinForms.ToolStripItem>()
                        .Where(item => item.Available)
                        .Select(item => new AppMenuItemViewModel(item))
                        .ToArray();
        }
    }

    /// <summary>
    /// One entry of the mirrored menu.
    /// </summary>
    public class AppMenuItemViewModel : INotifyPropertyChanged
    {
        private readonly WinForms.ToolStripItem source;

        internal AppMenuItemViewModel(WinForms.ToolStripItem source)
        {
            this.source = source;

            IsSeparator = source is WinForms.ToolStripSeparator;

            // HasDropDownItems first: reading DropDownItems allocates a drop-down for every leaf it is asked about.
            var parent = source as WinForms.ToolStripDropDownItem;
            if (parent != null && parent.HasDropDownItems)
            {
                Children = new ObservableCollection<AppMenuItemViewModel>(AppMenuViewModel.Mirror(parent.DropDownItems));
            }
            else if (!IsSeparator)
            {
                // Only leaves get a command. A submenu header with one would run it as well as open its submenu.
                // The check reads the item itself, so the command manager's re-query keeps it truthful even
                // between two refreshes from the shell.
                Command = new RelayCommand(source.PerformClick, () => source.Enabled);
            }

            Read();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsSeparator { get; }

        /// <summary>Null for a leaf, so the item template can tell a submenu header from a command.</summary>
        public ObservableCollection<AppMenuItemViewModel> Children { get; }

        public RelayCommand Command { get; }

        private string _header = string.Empty;
        public string Header
        {
            get => _header;
            private set => SetField(ref _header, value);
        }

        private string _toolTip;
        public string ToolTip
        {
            get => _toolTip;
            private set => SetField(ref _toolTip, value);
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            private set => SetField(ref _isEnabled, value);
        }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            private set => SetField(ref _isChecked, value);
        }

        internal void Refresh()
        {
            Read();

            if (Children != null)
            {
                foreach (var child in Children)
                {
                    child.Refresh();
                }
            }
        }

        private void Read()
        {
            Header = ToAccessText(source.Text);

            // Null rather than empty: an empty string still pops up an empty tooltip frame.
            ToolTip = string.IsNullOrWhiteSpace(source.ToolTipText) ? null : source.ToolTipText;
            IsEnabled = source.Enabled;
            IsChecked = (source as WinForms.ToolStripMenuItem)?.Checked ?? false;
        }

        /// <summary>
        /// Translates a WinForms caption into WPF access-key syntax.
        /// </summary>
        /// <remarks>
        /// WinForms marks the mnemonic with "&amp;" and escapes a literal one as "&amp;&amp;"; WPF marks it with "_",
        /// escapes a literal underscore as "__", and shows "&amp;" as itself. Copying the caption across unchanged
        /// would put stray ampersands in the menu and turn any underscore into a mnemonic.
        /// </remarks>
        internal static string ToAccessText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Underscores first, so the ones about to be written for the mnemonics are not doubled with them. The
            // escaped ampersand is parked on a control character while the single ones are converted.
            const string literalAmpersand = "";

            return text.Replace("_", "__")
                       .Replace("&&", literalAmpersand)
                       .Replace("&", "_")
                       .Replace(literalAmpersand, "&");
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (object.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
