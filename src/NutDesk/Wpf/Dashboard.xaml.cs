using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace NutDesk.Wpf
{
    /// <summary>
    /// The WPF dashboard that fills the main window's client area.
    /// </summary>
    /// <remarks>
    /// Hosted in the WinForms shell through an ElementHost. The palette is merged in as a dictionary so a theme
    /// change is a dictionary swap; the gauge brushes and the state lamps are the few things that cannot be
    /// expressed as a DynamicResource binding alone and are refreshed in code.
    /// </remarks>
    public partial class Dashboard : UserControl
    {
        /// <summary>Shared converter instance, referenced from the markup with x:Static.</summary>
        public static readonly BooleanToVisibilityConverter BoolVisibility = new BooleanToVisibilityConverter();

        private DashboardViewModel viewModel;
        private AppMenu appMenu;
        private bool paletteIsDark = true;

        public Dashboard()
        {
            InitializeComponent();

            // Index 0 is always the palette, so a theme change replaces exactly one dictionary.
            Resources.MergedDictionaries.Insert(0, Palette.Build(true));
            ApplyPalette(true);
        }

        public DashboardViewModel Model
        {
            get => viewModel;
            set
            {
                if (viewModel != null)
                {
                    viewModel.PropertyChanged -= OnModelPropertyChanged;
                }

                viewModel = value;
                DataContext = value;

                if (viewModel != null)
                {
                    viewModel.PropertyChanged += OnModelPropertyChanged;
                }

                RefreshVisualState();
            }
        }

        /// <summary>Swap the whole palette, then re-apply the colours that are set in code.</summary>
        public void ApplyPalette(bool isDark)
        {
            // Replacing the dictionary costs one invalidation pass. Clearing and re-filling the live one costs
            // a pass per key and leaves every DynamicResource unresolved while the refill is under way.
            Resources.MergedDictionaries[0] = Palette.Build(isDark);
            paletteIsDark = isDark;

            // The menu is the root of its own popup, outside this control's resources, so it is told separately.
            appMenu?.ApplyPalette(isDark);

            RefreshGaugeBrushes();
            RefreshVisualState();
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DashboardViewModel.ConnectionState):
                case nameof(DashboardViewModel.IsOnLine):
                case nameof(DashboardViewModel.IsOnBattery):
                case nameof(DashboardViewModel.IsOverload):
                case nameof(DashboardViewModel.IsBatteryLow):
                case nameof(DashboardViewModel.BatteryCharge):
                case nameof(DashboardViewModel.LoadPercent):
                    RefreshVisualState();
                    break;
            }
        }

        private void OnOverflowClick(object sender, RoutedEventArgs e)
        {
            var source = viewModel?.AppMenu;
            if (source == null)
            {
                return;
            }

            // Created on first use and then kept, so ApplyPalette has one instance to follow the theme with.
            if (appMenu == null)
            {
                appMenu = new AppMenu(paletteIsDark);
            }

            // Re-read on every opening: the shell adds entries to its menu after the dashboard is built.
            source.Rebuild();
            appMenu.DataContext = source;

            // Space and Enter raise Click too; this is how a keyboard activation is told from a mouse one.
            var fromKeyboard = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
            appMenu.Open(OverflowButton, fromKeyboard);
        }

        private Brush Brush(string key)
        {
            return TryFindResource(key) as Brush;
        }

        private void RefreshGaugeBrushes()
        {
            foreach (var gauge in new[] { BatteryGauge, LoadGauge, InputGauge, OutputGauge, BatteryVoltageGauge, FrequencyGauge })
            {
                gauge.TrackBrush = Brush(Palette.TrackBrushKey);
                gauge.ArcBrush = Brush(Palette.AccentBrushKey);
                gauge.PrimaryTextBrush = Brush(Palette.TextPrimaryBrushKey);
                gauge.SecondaryTextBrush = Brush(Palette.TextSecondaryBrushKey);
            }
        }

        /// <summary>
        /// Colour the things that carry meaning: the status dot, the four lamps, and the two gauges whose
        /// reading has a good and a bad end.
        /// </summary>
        private void RefreshVisualState()
        {
            if (viewModel == null || StatusDot == null)
            {
                return;
            }

            StatusDot.Fill = StatusBrush(viewModel.ConnectionState);

            LampOnLine.Fill = viewModel.IsOnLine ? Brush(Palette.SuccessBrushKey) : Brush(Palette.NeutralBrushKey);
            LampOnBattery.Fill = viewModel.IsOnBattery ? Brush(Palette.WarningBrushKey) : Brush(Palette.NeutralBrushKey);
            LampOverload.Fill = viewModel.IsOverload ? Brush(Palette.DangerBrushKey) : Brush(Palette.NeutralBrushKey);
            LampBatteryLow.Fill = viewModel.IsBatteryLow ? Brush(Palette.DangerBrushKey) : Brush(Palette.NeutralBrushKey);

            // Battery: low is bad. Load: high is bad.
            BatteryGauge.ArcBrush = BandBrush(viewModel.BatteryCharge <= 20.0, viewModel.BatteryCharge <= 40.0);
            LoadGauge.ArcBrush = BandBrush(viewModel.LoadPercent >= 95.0, viewModel.LoadPercent >= 80.0);
        }

        private Brush BandBrush(bool isCritical, bool isWarning)
        {
            if (isCritical)
            {
                return Brush(Palette.DangerBrushKey);
            }

            if (isWarning)
            {
                return Brush(Palette.WarningBrushKey);
            }

            return Brush(Palette.AccentBrushKey);
        }

        private Brush StatusBrush(DashboardViewModel.ConnectionKind state)
        {
            switch (state)
            {
                case DashboardViewModel.ConnectionKind.Online:
                    return Brush(Palette.SuccessBrushKey);
                case DashboardViewModel.ConnectionKind.OnBattery:
                case DashboardViewModel.ConnectionKind.Connecting:
                    return Brush(Palette.WarningBrushKey);
                case DashboardViewModel.ConnectionKind.Alert:
                    return Brush(Palette.DangerBrushKey);
                default:
                    return Brush(Palette.NeutralBrushKey);
            }
        }
    }
}
