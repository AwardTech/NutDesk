using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NutDesk.Wpf
{
    /// <summary>
    /// Everything the dashboard shows. The WinForms shell keeps this up to date; the view only binds to it.
    /// </summary>
    /// <remarks>
    /// Captions live here too, pushed in from the shell, because the translations are in the existing .resx
    /// files that the WinForms designer loads. That keeps all seven shipped cultures working without the XAML
    /// knowing anything about localization.
    /// </remarks>
    public class DashboardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (object.Equals(field, value))
            {
                return;
            }

            field = value;
            Raise(name);
        }

        #region Connection state

        public enum ConnectionKind
        {
            Offline,
            Connecting,
            Online,
            OnBattery,
            Alert
        }

        private ConnectionKind _connectionKind = ConnectionKind.Offline;
        public ConnectionKind ConnectionState
        {
            get => _connectionKind;
            set => SetField(ref _connectionKind, value);
        }

        private string _connectionText = string.Empty;
        public string ConnectionText
        {
            get => _connectionText;
            set => SetField(ref _connectionText, value);
        }

        private string _serverText = string.Empty;
        public string ServerText
        {
            get => _serverText;
            set => SetField(ref _serverText, value);
        }

        private string _upsNameText = string.Empty;
        public string UpsNameText
        {
            get => _upsNameText;
            set => SetField(ref _upsNameText, value);
        }

        private string _appTitle = string.Empty;
        public string AppTitle
        {
            get => _appTitle;
            set => SetField(ref _appTitle, value);
        }

        private bool _canDisconnect;
        public bool CanDisconnect
        {
            get => _canDisconnect;
            set
            {
                SetField(ref _canDisconnect, value);
                Raise(nameof(ShowConnect));
            }
        }

        /// <summary>Connect and Disconnect are never both applicable; only one is offered.</summary>
        public bool ShowConnect => !CanDisconnect;

        #endregion

        #region Readings

        private double _batteryCharge;
        public double BatteryCharge
        {
            get => _batteryCharge;
            set => SetField(ref _batteryCharge, value);
        }

        private string _batteryChargeText = "-";
        public string BatteryChargeText
        {
            get => _batteryChargeText;
            set => SetField(ref _batteryChargeText, value);
        }

        private string _runtimeText = string.Empty;
        public string RuntimeText
        {
            get => _runtimeText;
            set => SetField(ref _runtimeText, value);
        }

        private double _load;
        public double LoadPercent
        {
            get => _load;
            set => SetField(ref _load, value);
        }

        private string _loadText = "-";
        public string LoadText
        {
            get => _loadText;
            set => SetField(ref _loadText, value);
        }

        private string _powerText = string.Empty;
        public string PowerText
        {
            get => _powerText;
            set => SetField(ref _powerText, value);
        }

        private double _inputVoltage;
        public double InputVoltage
        {
            get => _inputVoltage;
            set => SetField(ref _inputVoltage, value);
        }

        private string _inputVoltageText = "-";
        public string InputVoltageText
        {
            get => _inputVoltageText;
            set => SetField(ref _inputVoltageText, value);
        }

        private double _outputVoltage;
        public double OutputVoltage
        {
            get => _outputVoltage;
            set => SetField(ref _outputVoltage, value);
        }

        private string _outputVoltageText = "-";
        public string OutputVoltageText
        {
            get => _outputVoltageText;
            set => SetField(ref _outputVoltageText, value);
        }

        private double _frequency;
        public double Frequency
        {
            get => _frequency;
            set => SetField(ref _frequency, value);
        }

        private string _frequencyText = "-";
        public string FrequencyText
        {
            get => _frequencyText;
            set => SetField(ref _frequencyText, value);
        }

        private double _batteryVoltage;
        public double BatteryVoltage
        {
            get => _batteryVoltage;
            set => SetField(ref _batteryVoltage, value);
        }

        private string _batteryVoltageText = "-";
        public string BatteryVoltageText
        {
            get => _batteryVoltageText;
            set => SetField(ref _batteryVoltageText, value);
        }

        #endregion

        #region Scale ranges

        private double _inputVoltageMin = 210;
        public double InputVoltageMin
        {
            get => _inputVoltageMin;
            set => SetField(ref _inputVoltageMin, value);
        }

        private double _inputVoltageMax = 270;
        public double InputVoltageMax
        {
            get => _inputVoltageMax;
            set => SetField(ref _inputVoltageMax, value);
        }

        private double _outputVoltageMin = 210;
        public double OutputVoltageMin
        {
            get => _outputVoltageMin;
            set => SetField(ref _outputVoltageMin, value);
        }

        private double _outputVoltageMax = 270;
        public double OutputVoltageMax
        {
            get => _outputVoltageMax;
            set => SetField(ref _outputVoltageMax, value);
        }

        private double _frequencyMin = 40;
        public double FrequencyMin
        {
            get => _frequencyMin;
            set => SetField(ref _frequencyMin, value);
        }

        private double _frequencyMax = 60;
        public double FrequencyMax
        {
            get => _frequencyMax;
            set => SetField(ref _frequencyMax, value);
        }

        private double _batteryVoltageMin = 6;
        public double BatteryVoltageMin
        {
            get => _batteryVoltageMin;
            set => SetField(ref _batteryVoltageMin, value);
        }

        private double _batteryVoltageMax = 18;
        public double BatteryVoltageMax
        {
            get => _batteryVoltageMax;
            set => SetField(ref _batteryVoltageMax, value);
        }

        #endregion

        #region Status flags

        private bool _isOnLine;
        public bool IsOnLine
        {
            get => _isOnLine;
            set => SetField(ref _isOnLine, value);
        }

        private bool _isOnBattery;
        public bool IsOnBattery
        {
            get => _isOnBattery;
            set => SetField(ref _isOnBattery, value);
        }

        private bool _isOverload;
        public bool IsOverload
        {
            get => _isOverload;
            set => SetField(ref _isOverload, value);
        }

        private bool _isBatteryLow;
        public bool IsBatteryLow
        {
            get => _isBatteryLow;
            set => SetField(ref _isBatteryLow, value);
        }

        #endregion

        #region Device

        private string _manufacturer = string.Empty;
        public string Manufacturer
        {
            get => _manufacturer;
            set => SetField(ref _manufacturer, value);
        }

        private string _model = string.Empty;
        public string Model
        {
            get => _model;
            set => SetField(ref _model, value);
        }

        private string _serial = string.Empty;
        public string Serial
        {
            get => _serial;
            set => SetField(ref _serial, value);
        }

        private string _firmware = string.Empty;
        public string Firmware
        {
            get => _firmware;
            set => SetField(ref _firmware, value);
        }

        #endregion

        #region Log

        private string _latestLogLine = string.Empty;
        public string LatestLogLine
        {
            get => _latestLogLine;
            set => SetField(ref _latestLogLine, value);
        }

        public ObservableCollection<string> LogHistory { get; } = new ObservableCollection<string>();

        #endregion

        #region Captions

        // Filled from the existing localized resources by the shell.
        public string CaptionBatteryCharge { get; set; } = "Battery charge";
        public string CaptionLoad { get; set; } = "Load";
        public string CaptionInputVoltage { get; set; } = "Input voltage";
        public string CaptionOutputVoltage { get; set; } = "Output voltage";
        public string CaptionFrequency { get; set; } = "Frequency";
        public string CaptionBatteryVoltage { get; set; } = "Battery voltage";
        public string CaptionRuntime { get; set; } = "Remaining time";
        public string CaptionManufacturer { get; set; } = "Manufacturer";
        public string CaptionModel { get; set; } = "Name";
        public string CaptionSerial { get; set; } = "Serial";
        public string CaptionFirmware { get; set; } = "Firmware";
        public string CaptionOnLine { get; set; } = "On line";
        public string CaptionOnBattery { get; set; } = "On battery";
        public string CaptionOverload { get; set; } = "Overload";
        public string CaptionBatteryLow { get; set; } = "Battery low";
        public string CaptionConnect { get; set; } = "Connect";
        public string CaptionDisconnect { get; set; } = "Disconnect";
        public string CaptionSettings { get; set; } = "Settings";
        public string CaptionUPSCommands { get; set; } = "UPS Commands";
        // Starts empty instead of holding an English placeholder: this one is a visible card heading, so the
        // untranslated word would show through until the shell pushes the localized caption.
        public string CaptionDevice { get; set; } = string.Empty;

        /// <summary>
        /// Accessible name and tooltip of the overflow button, whose visible content is a private-use icon
        /// glyph that a screen reader would otherwise announce as nothing at all.
        /// </summary>
        public string CaptionMoreOptions { get; set; } = string.Empty;

        /// <summary>Tell the view that every caption changed at once, after a language or resource refresh.</summary>
        public void RefreshCaptions()
        {
            foreach (var name in new[] { nameof(CaptionBatteryCharge), nameof(CaptionLoad), nameof(CaptionInputVoltage),
                                         nameof(CaptionOutputVoltage), nameof(CaptionFrequency), nameof(CaptionBatteryVoltage),
                                         nameof(CaptionRuntime), nameof(CaptionManufacturer), nameof(CaptionModel),
                                         nameof(CaptionSerial), nameof(CaptionFirmware), nameof(CaptionOnLine),
                                         nameof(CaptionOnBattery), nameof(CaptionOverload), nameof(CaptionBatteryLow),
                                         nameof(CaptionConnect), nameof(CaptionDisconnect), nameof(CaptionSettings),
                                         nameof(CaptionUPSCommands), nameof(CaptionDevice), nameof(CaptionMoreOptions) })
            {
                Raise(name);
            }
        }

        #endregion

        #region Commands

        public RelayCommand ConnectCommand { get; set; }
        public RelayCommand DisconnectCommand { get; set; }
        public RelayCommand SettingsCommand { get; set; }
        public RelayCommand UPSCommandsCommand { get; set; }

        #endregion

        #region Application menu

        /// <summary>The menu behind the header's overflow button, mirrored from the shell's WinForms menu.</summary>
        public AppMenuViewModel AppMenu { get; set; }

        #endregion
    }
}
