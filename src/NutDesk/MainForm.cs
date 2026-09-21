using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using NutDesk.Theming;
using NutDesk.Common;
using OldParams = NutDesk.Common.OldParams;
using Updater = NutDesk.Common.Updater;
using static NutDesk.Common.AppGlobals;

namespace NutDesk
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// The single live instance of the main window, assigned by Program.Main. Replaces the VB default form
        /// instance that call sites such as "MainForm.UPS_Device" used to reach.
        /// </summary>
        internal static MainForm Instance { get; set; }

        #region "Properties"

        public bool HasCrashed
        {
            set => NutDesk_Crashed = value;
        }

        #endregion

        // Never reassigned, so a plain field plus an explicit subscription in WireEvents replaces VB's WithEvents.
        private readonly Logger LogFile = AppGlobals.LogFile;

        private UPS_Device _upsDevice;

        /// <summary>
        /// Reproduces VB's "Public WithEvents UPS_Device": assigning a new device detaches every handler from the
        /// previous instance and attaches them to the new one. TearDownUPSDevice and UPS_Connect rely on this.
        /// </summary>
        public UPS_Device UPS_Device
        {
            get => _upsDevice;
            set
            {
                if (_upsDevice != null)
                {
                    FollowDeviceNotifications(_upsDevice, false);
                    _upsDevice.Connected -= UPSReady;
                    _upsDevice.ConnectionError -= ConnectionError;
                    _upsDevice.Lost_Connect -= UPS_Lostconnect;
                    _upsDevice.Reconnecting -= UPS_Reconnecting;
                    _upsDevice.ReconnectAbandoned -= UPS_ReconnectAbandoned;
                    _upsDevice.Disconnected -= UPSDisconnectedEvent;
                    _upsDevice.DataUpdated -= Update_UPS_Data;
                    _upsDevice.StatusesChanged -= HandleUPSStatusChange;
                }

                _upsDevice = value;

                if (_upsDevice != null)
                {
                    // Attached ahead of the shell's own handlers, which the device invokes in subscription order:
                    // the shell reacts to an FSD or to the battery limits by starting a shutdown, and the user
                    // should be told the power went out before being told the computer is going down because of it.
                    FollowDeviceNotifications(_upsDevice, true);
                    _upsDevice.Connected += UPSReady;
                    _upsDevice.ConnectionError += ConnectionError;
                    _upsDevice.Lost_Connect += UPS_Lostconnect;
                    _upsDevice.Reconnecting += UPS_Reconnecting;
                    _upsDevice.ReconnectAbandoned += UPS_ReconnectAbandoned;
                    _upsDevice.Disconnected += UPSDisconnectedEvent;
                    _upsDevice.DataUpdated += Update_UPS_Data;
                    _upsDevice.StatusesChanged += HandleUPSStatusChange;
                }
            }
        }

        /// <summary>
        /// Toast and email notifications. Null until Load has created it and again once the application has
        /// really exited; every use checks for null, so an early event or a failed creation costs nothing.
        /// </summary>
        private Notifications.NotificationCenter notifications;

        // Variable used for icon
        private int LastAppIconIdx = -1;
        private int ActualAppIconIdx;
        private bool WinDarkMode = false;
        private bool AppDarkMode = false;

        public string UPS_Mfr;
        public string UPS_Model;
        public string UPS_Serial;
        public string UPS_Firmware;
        public double UPS_BattCh;
        public double UPS_BattV;
        public double UPS_BattRuntime;
        public double UPS_BattCapacity;
        public double UPS_InputF;
        public double UPS_InputV;
        public double UPS_OutputV;
        public double UPS_Load;
        public string UPS_Status;
        public double UPS_OutPower;
        public double UPS_InputA;


        private bool HasFocus = true;
        private string FormText;
        private bool NutDesk_Crashed = false;

        // Events Declaration
        private delegate void UpdateNotifyIconStrEventHandler(string Reason, string Message);
        private delegate void UpdateBatteryStateEventHandler(string Reason);

        // private event Data_UpdatedEventHandler Data_Updated;
        private event UpdateNotifyIconStrEventHandler UpdateNotifyIconStr;
        private event UpdateBatteryStateEventHandler UpdateBatteryState;
        // UPS object operation

        public MainForm()
        {
            InitializeComponent();
            WireEvents();
        }

        /// <summary>
        /// Everything VB expressed with "Handles" clauses. Kept out of the designer file so the layout code stays
        /// free of behaviour, and so the set of subscriptions is readable in one place.
        /// </summary>
        private void WireEvents()
        {
            Load += NutDesk_Load;
#if DEBUG
            Load += InsertDebugMenuOnLoad;
#endif
            Activated += NutDesk_Activated;
            Shown += NutDesk_Shown;
            FormClosing += NutDesk_FormClosing;
            FormClosed += NutDesk_FormClosed;
            Resize += NutDesk_Resize;
            Deactivate += NutDesk_Deactivate;

            LogFile.DisplayedLogsLineAdded += AddLogLine;
            LogFile.DisplayedLogsTrimmed += TrimLogLine;

            UpdateNotifyIconStr += Event_UpdateNotifyIconStr;
            UpdateBatteryState += Event_UpdateBatteryState;

            Menu_Quit.Click += Menu_Quit_Click_1;
            Menu_Sys_Exit.Click += Menu_Sys_Exit_Click;
            NotifyIcon.MouseClick += NotifyIcon_MouseClick;
            NotifyIcon.MouseDoubleClick += NotifyIcon_MouseClick;
            Menu_About.Click += Menu_About_Click;
            Menu_Sys_About.Click += Menu_Sys_About_Click;
            Menu_Disconnect.Click += Menu_Disconnect_Click;
            Menu_Connect.Click += Menu_Reconnect_Click;
            Menu_Sys_Settings.Click += (sender, e) => OpenPrefsForm();
            Menu_Settings.Click += (sender, e) => OpenPrefsForm();
            Menu_UPS_Var.Click += Menu_UPS_Var_Click;
            ManageOldPrefsToolStripMenuItem.Click += ManageOldPrefsToolStripMenuItem_Click;
            Menu_Update.Click += Menu_Update_Click;
            // The tray menu has always had this item, but WinNUT never connected it to anything.
            Menu_Sys_Update.Click += Menu_Update_Click;
            Menu_Persist.CheckedChanged += Menu_Persist_CheckedChanged;
        }

        #region "Form Event Handling"

        private void NutDesk_Load(object sender, EventArgs e)
        {
            // Add Main Gui's Strings
            StrLog.Insert((int)AppResxStr.STR_MAIN_RECONNECT, Properties.Resources.Frm_Main_Str_03);
            StrLog.Insert((int)AppResxStr.STR_MAIN_NOTCONN, Properties.Resources.Frm_Main_Str_05);
            StrLog.Insert((int)AppResxStr.STR_MAIN_CONN, Properties.Resources.Frm_Main_Str_06);
            StrLog.Insert((int)AppResxStr.STR_MAIN_OL, Properties.Resources.Frm_Main_Str_07);
            StrLog.Insert((int)AppResxStr.STR_MAIN_OB, Properties.Resources.Frm_Main_Str_08);
            StrLog.Insert((int)AppResxStr.STR_MAIN_LOWBAT, Properties.Resources.Frm_Main_Str_09);
            StrLog.Insert((int)AppResxStr.STR_MAIN_BATOK, Properties.Resources.Frm_Main_Str_10);
            StrLog.Insert((int)AppResxStr.STR_MAIN_UNKNOWN_UPS, Properties.Resources.Frm_Main_Str_11);
            StrLog.Insert((int)AppResxStr.STR_MAIN_LOSTCONNECT, Properties.Resources.Frm_Main_Str_12);
            StrLog.Insert((int)AppResxStr.STR_MAIN_INVALIDLOGIN, Properties.Resources.Frm_Main_Str_13);
            StrLog.Insert((int)AppResxStr.STR_MAIN_EXITSLEEP, Properties.Resources.Frm_Main_Str_14);
            StrLog.Insert((int)AppResxStr.STR_MAIN_GOTOSLEEP, Properties.Resources.Frm_Main_Str_15);

            // Add Shutdown Gui's Strings
            StrLog.Insert((int)AppResxStr.STR_SHUT_STAT, Properties.Resources.Frm_Shutdown_Str_01);

            // Add App Event's Strings
            StrLog.Insert((int)AppResxStr.STR_APP_SHUT, Properties.Resources.App_Event_Str_01);

            // Add Log's Strings
            StrLog.Insert((int)AppResxStr.STR_LOG_PREFS, Properties.Resources.Log_Str_01);
            StrLog.Insert((int)AppResxStr.STR_LOG_CONNECTED, Properties.Resources.Log_Str_02);
            StrLog.Insert((int)AppResxStr.STR_LOG_CON_FAILED, Properties.Resources.Log_Str_03);
            StrLog.Insert((int)AppResxStr.STR_LOG_CON_RETRY, Properties.Resources.Log_Str_04);
            StrLog.Insert((int)AppResxStr.STR_LOG_LOGOFF, Properties.Resources.Log_Str_05);
            StrLog.Insert((int)AppResxStr.STR_LOG_SHUT_START, Properties.Resources.Log_Str_08);
            StrLog.Insert((int)AppResxStr.STR_LOG_SHUT_STOP, Properties.Resources.Log_Str_09);
            StrLog.Insert((int)AppResxStr.STR_LOG_NO_UPDATE, Properties.Resources.Log_Str_10);
            StrLog.Insert((int)AppResxStr.STR_LOG_UPDATE, Properties.Resources.Log_Str_11);
            StrLog.Insert((int)AppResxStr.STR_LOG_NUT_FSD, Properties.Resources.Log_Str_12);
            StrLog.Insert((int)AppResxStr.STR_MAIN_CONNECTING, Properties.Resources.Frm_Main_Str_Connecting);

            // Init Systray
            NotifyIcon.Text = ProgramName + " - " + ShortProgramVersion;
            NotifyIcon.Visible = false;
            LogFile.LogTracing("NotifyIcons Initialised", LogLvl.LOG_DEBUG, this);

            // Here rather than in the constructor: the center captures the UI thread and its callbacks reach the
            // window and the tray icon, so it needs the handle, the settings Program.Startup loaded and a tray icon
            // that has been set up. And before the update check below, whose result it reports.
            CreateNotificationCenter();

            // The first start after an in-place update says so. Program.Startup has logged it already.
            if (Program.UpdatedFromVersion != null)
            {
                Notify(center => center.NotifyUpdated(Program.UpdatedFromVersion, Updater.UpdateUtil.RunningVersionText),
                       "the completed update");
            }

            // UPS_Device.Battery_Limit = NutDesk_Params.Arr_Reg_Key.Item("ShutdownLimitBatteryCharge")
            // UPS_Device.Backup_Limit = NutDesk_Params.Arr_Reg_Key.Item("ShutdownLimitUPSRemainTime")
            // UPS_Device.UPS_Follow_FSD = NutDesk_Params.Arr_Reg_Key.Item("Follow_FSD")

            // Verify Is Windows is In Dark Mode
            if (Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0)) == 0)
            {
                LogFile.LogTracing("Windows App Use Dark Theme", LogLvl.LOG_DEBUG, this);
                AppDarkMode = true;
            }
            else
            {
                LogFile.LogTracing("Windows App Use Light Theme", LogLvl.LOG_DEBUG, this);
                AppDarkMode = false;
            }
            if (Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0)) == 0)
            {
                LogFile.LogTracing("Windows Use Dark Theme", LogLvl.LOG_DEBUG, this);
                WinDarkMode = true;
            }
            else
            {
                LogFile.LogTracing("Windows Use Light Theme", LogLvl.LOG_DEBUG, this);
                WinDarkMode = false;
            }

            // Adapts the icon according to the Win / App Dark Mode
            int Start_App_Icon;
            int Start_Tray_Icon;
            if (AppDarkMode)
            {
                Start_App_Icon = (int)(AppIconIdx.IDX_ICO_OFFLINE | AppIconIdx.WIN_DARK | AppIconIdx.IDX_OFFSET);
            }
            else
            {
                Start_App_Icon = (int)(AppIconIdx.IDX_ICO_OFFLINE | AppIconIdx.IDX_OFFSET);
            }
            Icon = GetIcon(Start_App_Icon);
            if (WinDarkMode)
            {
                Start_Tray_Icon = (int)(AppIconIdx.IDX_ICO_OFFLINE | AppIconIdx.WIN_DARK | AppIconIdx.IDX_OFFSET);
            }
            else
            {
                Start_Tray_Icon = (int)(AppIconIdx.IDX_ICO_OFFLINE | AppIconIdx.IDX_OFFSET);
            }

            // Initializes the state of the NotifyICon, the connection to the Nut server and the application icons
            NotifyIcon.Visible = false;
            // The frame drawn for the notification area's own size: the default frame is shrunk and blurred there.
            NotifyIcon.Icon = GetIcon(Start_Tray_Icon, SystemInformation.SmallIconSize);
            UpdateNotifyIconStr?.Invoke(null, null);
            ActualAppIconIdx = Start_App_Icon;
            UpdateIcon_NotifyIcon();
            LogFile.LogTracing("Update Icon at Startup", LogLvl.LOG_DEBUG, this);
            // Start_Tray_Icon = Nothing

            UpdateMainMenuState();
            ReInitDisplayValues();

            // The automatic update checks: the first a minute from now, then whenever one falls due.
            StartUpdateCheckTimer();

            ApplyModernLook();

            // Prepare visual log combo box. Replayed only once the dashboard exists, because AddLogLine reaches the
            // view model only after DashboardModel has been created - anything logged earlier in this handler, such as
            // the auto update check, otherwise left the status bar blank.
            foreach (var line in LogFile.DisplayedLogs)
            {
                AddLogLine(line);
            }

            Microsoft.Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            Properties.Settings.Default.PropertyChanged += OnPropertyChanged;
            Wpf.PreferencesWindow.SavedPreferences += ResetUIState;

            LogFile.LogTracing("NutDesk Form completed Load.", LogLvl.LOG_NOTICE, this);
        }

        #region "Look and feel"

        private ToolStripMenuItem ThemeMenu;
        private readonly Dictionary<Theme.Preference, ToolStripMenuItem> ThemeMenuItems = new Dictionary<Theme.Preference, ToolStripMenuItem>();
        private ToolStripMenuItem LogMenuItem;
        private Wpf.LogWindow LogView;

        /// <summary>
        /// Applies the modern palette, typeface and chrome on top of the designer layout. Done in code rather than
        /// in the designer so that the seven localized .resx files, which carry every control's position and size,
        /// stay untouched.
        /// </summary>
        private void ApplyModernLook()
        {
            Theme.IsDark = Theme.ResolveIsDark(CurrentThemePreference());
            AppDarkMode = Theme.IsDark;
            LogFile.LogTracing("Applying " + (Theme.IsDark ? "dark" : "light") + " theme.", LogLvl.LOG_DEBUG, this);

            // Built before the dashboard so the menu is complete the first time it is mirrored.
            BuildLogMenu();
            BuildThemeMenu();

            EnsureDashboard();

            BackColor = Theme.WindowBack;
            ForeColor = Theme.TextPrimary;
            Theme.ApplyTitleBarTheme(this);
            DashboardView.ApplyPalette(Theme.IsDark);

            // The log and update windows are separate top-level windows, so nothing else would repaint them.
            LogView?.ApplyPalette(Theme.IsDark);
            UpdateView?.ApplyPalette(Theme.IsDark);

            CopyCaptionsToDashboard();
            RefreshDashboardChrome();
            PushRangesToDashboard();
            PushReadingsToDashboard();

            // Seeds the badge on the first build, and keeps it truthful on every later theme change. This used
            // to replay UpdateNotifyIconStr with no reason, which fell through to the "not connected" branch
            // and wiped the badge of a live connection.
            RefreshStatusPill();
        }

        private Theme.Preference CurrentThemePreference()
        {
            switch (Properties.Settings.Default.UI_Theme)
            {
                case 1:
                    return Theme.Preference.Light;
                case 2:
                    return Theme.Preference.Dark;
                default:
                    return Theme.Preference.System;
            }
        }

        /// <summary>
        /// Adds a Theme submenu under Settings. Built in code so the seven localized menu .resx files do not have
        /// to be touched to gain a new item.
        /// </summary>
        private void BuildThemeMenu()
        {
            if (ThemeMenu != null)
            {
                RefreshThemeMenuChecks();
                return;
            }

            ThemeMenu = new ToolStripMenuItem(Properties.Resources.Menu_Theme);

            foreach (var entry in new Dictionary<Theme.Preference, string>
            {
                { Theme.Preference.System, Properties.Resources.Theme_FollowWindows },
                { Theme.Preference.Light, Properties.Resources.Theme_Light },
                { Theme.Preference.Dark, Properties.Resources.Theme_Dark }
            })
            {
                var choice = entry.Key;
                var item = new ToolStripMenuItem(entry.Value) { CheckOnClick = false };
                item.Click += (sender, e) => SelectTheme(choice);
                ThemeMenu.DropDownItems.Add(item);
                ThemeMenuItems[choice] = item;
            }

            RefreshThemeMenuChecks();

            // Sits next to the existing Settings entry rather than inside the modal preferences dialog, so the
            // effect is visible immediately.
            var settingsIndex = Main_Menu.Items.IndexOf(Menu_Settings);
            if (settingsIndex >= 0)
            {
                Main_Menu.Items.Insert(settingsIndex + 1, ThemeMenu);
            }
            else
            {
                Main_Menu.Items.Add(ThemeMenu);
            }
        }

        /// <summary>
        /// Adds the event-log entry to the menu. Built in code for the same reason as the Theme submenu: the
        /// eight localized menu .resx files do not have to be touched to gain a new item.
        /// </summary>
        private void BuildLogMenu()
        {
            if (LogMenuItem != null)
            {
                return;
            }

            LogMenuItem = new ToolStripMenuItem(Properties.Resources.Menu_Log);
            LogMenuItem.Click += (sender, e) => ShowLogWindow();

            // Next to the UPS variables entry: both open a window that inspects what the client knows.
            var varIndex = Main_Menu.Items.IndexOf(Menu_UPS_Var);
            if (varIndex >= 0)
            {
                Main_Menu.Items.Insert(varIndex + 1, LogMenuItem);
            }
            else
            {
                Main_Menu.Items.Add(LogMenuItem);
            }
        }

        /// <summary>
        /// Opens the event-log window, or brings the existing one forward.
        /// </summary>
        /// <remarks>
        /// The log used to be a combo box on the form that the WPF dashboard replaced, which left no way to
        /// see the history of connections, power source changes and failures. One window is kept alive at a
        /// time; it unsubscribes from the logger when it closes, so a closed one costs nothing.
        /// </remarks>
        private void ShowLogWindow()
        {
            if (LogView != null)
            {
                if (LogView.WindowState == System.Windows.WindowState.Minimized)
                {
                    LogView.WindowState = System.Windows.WindowState.Normal;
                }

                LogView.Activate();
                return;
            }

            LogFile.LogTracing("Opening the event log window.", LogLvl.LOG_DEBUG, this);

            try
            {
                LogView = new Wpf.LogWindow(LogFile, Theme.IsDark);
                LogView.Closed += (s, e) => LogView = null;

                // Owning it to the main form keeps it above the shell and makes it close with it, without the
                // window being modal.
                new System.Windows.Interop.WindowInteropHelper(LogView).Owner = Handle;
                LogView.Show();
            }
            catch (Exception ex)
            {
                // A menu action must not be able to take the application down.
                LogFile.LogTracing("Unable to open the event log window.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);
                LogView = null;
            }
        }

        private void RefreshThemeMenuChecks()
        {
            var current = CurrentThemePreference();

            foreach (var pair in ThemeMenuItems)
            {
                pair.Value.Checked = (pair.Key == current);
            }

            AppMenuModel?.Refresh();
        }

        private void SelectTheme(Theme.Preference choice)
        {
            if (CurrentThemePreference() == choice)
            {
                return;
            }

            Properties.Settings.Default.UI_Theme = (int)choice;
            Properties.Settings.Default.Save();
            LogFile.LogTracing("Theme preference changed to " + choice.ToString() + ".", LogLvl.LOG_NOTICE, this);

            ApplyModernLook();
            ReapplyThemeToOpenForms();
            Refresh();
        }

        /// <summary>
        /// Restyle any other window that is currently open, so the change is not limited to the main form.
        /// </summary>
        private void ReapplyThemeToOpenForms()
        {
            foreach (Form frm in Application.OpenForms)
            {
                if (frm != this)
                {
                    Theme.Apply(frm);
                    frm.Refresh();
                }
            }
        }

        #endregion

        #region "WPF dashboard"

        private ElementHost WpfHost;
        private Wpf.Dashboard DashboardView;
        private Wpf.DashboardViewModel DashboardModel;
        private Wpf.AppMenuViewModel AppMenuModel;

        /// <summary>
        /// Replace the designer's control grid with the WPF dashboard, once.
        /// </summary>
        /// <remarks>
        /// The WinForms controls are kept alive but hidden: the rest of this form still writes to them (and reads
        /// Lbl_VRTime for the formatted runtime), and keeping them avoids touching the seven localized .resx files
        /// that hold their captions. Those captions are copied into the view model, so the WPF surface is localized
        /// by exactly the same resources as before.
        /// </remarks>
        private void EnsureDashboard()
        {
            if (WpfHost != null)
            {
                return;
            }

            HideMenuStrip();

            AppMenuModel = new Wpf.AppMenuViewModel(Main_Menu.Items);

            DashboardModel = new Wpf.DashboardViewModel
            {
                AppTitle = AppGlobals.ProgramName,
                ConnectCommand = new Wpf.RelayCommand(OnDashboardConnect),
                DisconnectCommand = new Wpf.RelayCommand(OnDashboardDisconnect),
                SettingsCommand = new Wpf.RelayCommand(OnDashboardSettings),
                AppMenu = AppMenuModel
            };

            CopyCaptionsToDashboard();

            DashboardView = new Wpf.Dashboard { Model = DashboardModel };

            WpfHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = DashboardView
            };

            SuspendLayout();

            try
            {
                foreach (Control legacy in Controls)
                {
                    legacy.Visible = false;
                }

                Controls.Add(WpfHost);
                WpfHost.BringToFront();

                MakeWindowResizable();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        /// <summary>
        /// Hide the menu bar: the header's overflow button opens a WPF mirror of it instead.
        /// </summary>
        /// <remarks>
        /// The items stay in Main_Menu, because they are what that mirror reads and clicks - their captions come
        /// from the localized .resx files and their Click handlers, Checked state and enabling logic all live here.
        /// The strip only stops being shown, and stops being the form's main menu so Alt does not reach for it.
        /// </remarks>
        private void HideMenuStrip()
        {
            Main_Menu.Visible = false;
            MainMenuStrip = null;
        }

        /// <summary>
        /// Give the window a sensible opening size and let the user take it from there.
        /// </summary>
        /// <remarks>
        /// Sized as a share of the work area rather than from a DPI factor. The reliable ways to ask for the scale
        /// disagree on this kind of setup - Control.DeviceDpi and a device context both report 96 on a display the
        /// shell is scaling - and a dashboard that opens at a third of the screen because of that is worse than one
        /// that simply takes a sensible proportion of whatever space there is.
        /// </remarks>
        private void MakeWindowResizable()
        {
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;

            // Measured before anything is clamped: the sizes below are client sizes, while WorkingArea also covers the
            // border and title bar, so clamping against the raw work area still leaves the window wider than the desktop.
            var chromeWidth = Width - ClientSize.Width;
            var chromeHeight = Height - ClientSize.Height;

            var work = Screen.FromControl(this).WorkingArea;
            var maxClientW = Math.Max(work.Width - chromeWidth, 1);
            var maxClientH = Math.Max(work.Height - chromeHeight, 1);

            // The floors have to give way to the work area on a small or heavily scaled display: raising the size to
            // them regardless produced a window larger than the desktop, which CenterToScreen pushed off-screen and
            // MinimumSize then pinned there.
            var width = Convert.ToInt32(Math.Min(Math.Max(work.Width * 0.62, 1000), maxClientW));
            var height = Convert.ToInt32(Math.Min(Math.Max(work.Height * 0.66, 620), maxClientH));

            ClientSize = new Size(width, height);
            CenterToScreen();

            MinimumSize = new Size(Math.Min(880, maxClientW) + chromeWidth, Math.Min(560, maxClientH) + chromeHeight);
        }

        /// <summary>
        /// Copy the localized captions out of the designer controls and into the view model.
        /// </summary>
        private void CopyCaptionsToDashboard()
        {
            if (DashboardModel == null)
            {
                return;
            }

            var model = DashboardModel;
            model.CaptionBatteryCharge = Lbl_BattCh_Dial.Text?.Trim();
            model.CaptionLoad = Lbl_Load_Dial.Text?.Trim();
            model.CaptionInputVoltage = Lbl_InV_Dial.Text?.Trim();
            model.CaptionOutputVoltage = Lbl_OutV_Dial.Text?.Trim();
            model.CaptionFrequency = Lbl_InF_Dial.Text?.Trim();
            model.CaptionBatteryVoltage = Lbl_BattV_Dial.Text?.Trim();
            model.CaptionRuntime = StripTrailingColon(Lbl_RTime.Text);
            // No designer control carries the Device card's heading, so it comes straight from the resources. The
            // card used to head itself with the configured UPS name, which is blank until the user sets one.
            model.CaptionDevice = Properties.Resources.Dashboard_CaptionDevice;
            model.CaptionMoreOptions = Properties.Resources.Dashboard_MoreOptions;
            model.CaptionManufacturer = StripTrailingColon(Lbl_Mfr.Text);
            model.CaptionModel = StripTrailingColon(Lbl_Name.Text);
            model.CaptionSerial = StripTrailingColon(Lbl_Serial.Text);
            model.CaptionFirmware = StripTrailingColon(Lbl_Firmware.Text);
            model.CaptionOnLine = Lbl_OL.Text?.Trim();
            model.CaptionOnBattery = Lbl_OB.Text?.Trim();
            model.CaptionOverload = Lbl_OLoad.Text?.Trim();
            model.CaptionBatteryLow = Lbl_BL.Text?.Trim();
            model.CaptionConnect = StripAccelerator(Menu_Connect.Text);
            model.CaptionDisconnect = StripAccelerator(Menu_Disconnect.Text);
            model.CaptionSettings = StripAccelerator(Menu_Settings.Text);
            model.RefreshCaptions();
        }

        /// <summary>The designer captions end in " :", which the card layout supplies itself.</summary>
        private static string StripTrailingColon(string text) =>
            (text ?? string.Empty).TrimEnd(' ', ':').Trim();

        private static string StripAccelerator(string text) =>
            (text ?? string.Empty).Replace("&", string.Empty).Trim();

        #region "Dashboard commands"

        private void OnDashboardConnect()
        {
            Menu_Connect.PerformClick();
        }

        private void OnDashboardDisconnect()
        {
            Menu_Disconnect.PerformClick();
        }

        private void OnDashboardSettings()
        {
            Menu_Settings.PerformClick();
        }

        #endregion

        #region "Dashboard updates"

        /// <summary>Push the current readings into the view model.</summary>
        private void PushReadingsToDashboard()
        {
            if (DashboardModel == null)
            {
                return;
            }

            var connected = UPS_Device?.IsConnected ?? false;

            var model = DashboardModel;
            model.BatteryCharge = connected ? Clamp(UPS_BattCh, 0, 100) : 0;
            model.BatteryChargeText = FormatReading(UPS_BattCh, 0, " %", connected);

            model.LoadPercent = connected ? Clamp(UPS_Load, 0, 100) : 0;
            model.LoadText = FormatReading(UPS_Load, 0, " %", connected);
            // Blank rather than a dash: this is the gauge's secondary line, and any text at all there shifts the
            // load reading up to make room, so a UPS that never reports output power kept a stray dash under it.
            model.PowerText = connected && UPS_OutPower > 0 ? FormatReading(UPS_OutPower, 0, " W", true) : string.Empty;

            model.InputVoltage = connected ? UPS_InputV : model.InputVoltageMin;
            model.InputVoltageText = FormatReading(UPS_InputV, 1, " V", connected);

            model.OutputVoltage = connected ? UPS_OutputV : model.OutputVoltageMin;
            model.OutputVoltageText = FormatReading(UPS_OutputV, 1, " V", connected);

            model.Frequency = connected ? UPS_InputF : model.FrequencyMin;
            model.FrequencyText = FormatReading(UPS_InputF, 1, " Hz", connected);

            model.BatteryVoltage = connected ? UPS_BattV : model.BatteryVoltageMin;
            model.BatteryVoltageText = FormatReading(UPS_BattV, 1, " V", connected);

            model.RuntimeText = connected ? Lbl_VRTime.Text : string.Empty;

            // Read from the device: the module-level UPS_Mfr and friends are only ever blanked, never filled.
            var identity = UPS_Device?.UPS_Datas;
            model.Manufacturer = connected && identity != null ? identity.Mfr : string.Empty;
            model.Model = connected && identity != null ? identity.Model : string.Empty;
            model.Serial = connected && identity != null ? identity.Serial : string.Empty;
            model.Firmware = connected && identity != null ? identity.Firmware : string.Empty;

            var status = UPS_Device?.UPS_Datas?.UPS_Value?.UPS_Status;
            model.IsOnLine = connected && status.HasValue && status.Value.HasFlag(UPS_States.OL);
            model.IsOnBattery = connected && status.HasValue && status.Value.HasFlag(UPS_States.OB);
            model.IsOverload = connected && status.HasValue && status.Value.HasFlag(UPS_States.OVER);
            model.IsBatteryLow = connected && status.HasValue && status.Value.HasFlag(UPS_States.LB);
        }

        /// <summary>Push the scale ranges the user configured in the calibration preferences.</summary>
        private void PushRangesToDashboard()
        {
            if (DashboardModel == null)
            {
                return;
            }

            var model = DashboardModel;
            model.InputVoltageMin = Properties.Settings.Default.CAL_VoltInMin;
            model.InputVoltageMax = Properties.Settings.Default.CAL_VoltInMax;
            model.OutputVoltageMin = Properties.Settings.Default.CAL_VoltOutMin;
            model.OutputVoltageMax = Properties.Settings.Default.CAL_VoltOutMax;
            model.FrequencyMin = Properties.Settings.Default.CAL_FreqInMin;
            model.FrequencyMax = Properties.Settings.Default.CAL_FreqInMax;
            model.BatteryVoltageMin = Properties.Settings.Default.CAL_BattVMin;
            model.BatteryVoltageMax = Properties.Settings.Default.CAL_BattVMax;
        }

        /// <summary>Refresh the parts of the header that are not readings.</summary>
        private void RefreshDashboardChrome()
        {
            if (DashboardModel == null)
            {
                return;
            }

            var model = DashboardModel;
            model.ServerText = string.IsNullOrWhiteSpace(Properties.Settings.Default.NUT_ServerAddress)
                ? string.Empty
                : string.Format("{0}:{1}", Properties.Settings.Default.NUT_ServerAddress, Properties.Settings.Default.NUT_ServerPort);
            model.UpsNameText = string.IsNullOrWhiteSpace(Properties.Settings.Default.NUT_UPSName) ? string.Empty : Properties.Settings.Default.NUT_UPSName;
            model.CanDisconnect = Menu_Disconnect.Enabled;
        }

        private static double Clamp(double value, double low, double high)
        {
            return Math.Max(low, Math.Min(high, value));
        }

        /// <summary>
        /// Format a reading for display, or a dash when there is nothing meaningful to show.
        /// </summary>
        private static string FormatReading(double value, int decimals, string unit,
                                            bool available)
        {
            if (!available || double.IsNaN(value) || value < 0)
            {
                return "-";
            }

            return Math.Round(value, decimals).ToString($"N{decimals}", CultureInfo.CurrentCulture) + unit;
        }

        #endregion

        #endregion

        #region "Debug menu"
#if DEBUG
        private void TestFillAndTrim()
        {
            for (int i = LogFile.DisplayedLogs.Count; i <= Logger.MAX_DISPLAYED_LOGS + 3; i++)
            {
                LogFile.LogTracing("Test logging line " + i, LogLvl.LOG_DEBUG, this, "Test logging line " + i);
            }
        }

        private void InsertDebugMenuOnLoad(object sender, EventArgs e)
        {
            var testFillAndTrimCommand = new ToolStripMenuItem("Test Fill and Trim");
            testFillAndTrimCommand.Click += (s, args) => TestFillAndTrim();

            var logDisplaySubmenu = new ToolStripMenuItem("LogDisplay");
            logDisplaySubmenu.DropDownItems.Add(testFillAndTrimCommand);

            // The automatic check without waiting for the timer. With NUTDESK_UPDATE_API_URL pointing at a local
            // stand-in for GitHub, this exercises the whole update path.
            var automaticUpdateCheckCommand = new ToolStripMenuItem("Automatic update check now");
            automaticUpdateCheckCommand.Click += (s, args) => RunAutomaticUpdateCheck(true);

            var updateSubmenu = new ToolStripMenuItem("Update");
            updateSubmenu.DropDownItems.Add(automaticUpdateCheckCommand);

            var debugMenu = new ToolStripMenuItem("Debug");
            debugMenu.DropDownItems.Add(logDisplaySubmenu);
            debugMenu.DropDownItems.Add(updateSubmenu);

            // Main_Menu is hidden by now, but it is still what the dashboard's menu mirrors, and that mirror is
            // re-read every time it opens - so this lands in the menu whichever order the Load handlers run in.
            Main_Menu.Items.Add(debugMenu);

            LogFile.LogTracing("Inserted debug menu into the application menu.", LogLvl.LOG_DEBUG, this, "Debug Menu enabled.");
        }
#endif
        #endregion

        /// <summary>
        /// Second-to-last step in loading the Form. "Occurs when the form is activated in code or by the user."
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NutDesk_Activated(object sender, EventArgs e)
        {
            LogFile.LogTracing("Main GUI activated.", LogLvl.LOG_DEBUG, this);

            if (NutDesk_Crashed)
            {
                Hide();

                return;
            }

            HasFocus = true;

            // TODO: Merge icon update code into single method.
            int Tmp_App_Mode;
            if (AppDarkMode)
            {
                Tmp_App_Mode = (int)(AppIconIdx.WIN_DARK | AppIconIdx.IDX_OFFSET);
            }
            else
            {
                Tmp_App_Mode = (int)AppIconIdx.IDX_OFFSET;
            }
            var TmpGuiIDX = ActualAppIconIdx | Tmp_App_Mode;
            Icon = GetIcon(TmpGuiIDX);
            LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
            UpdateIcon_NotifyIcon();
        }

        /// <summary>
        /// Final step in loading the main form for the first time.
        /// "The Shown event is _only_ raised the first time a form is displayed"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NutDesk_Shown(object sender, EventArgs e)
        {
            LogFile.LogTracing("NutDesk_Shown for the first time.", LogLvl.LOG_DEBUG, this);

            LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
            UpdateIcon_NotifyIcon();
            if (Properties.Settings.Default.MinimizeToTray == true & Properties.Settings.Default.MinimizeOnStart == true)
            {
                LogFile.LogTracing("Minimize NutDesk On Start", LogLvl.LOG_DEBUG, this);
                WindowState = FormWindowState.Minimized;
                NotifyIcon.Visible = true;
            }
            else
            {
                LogFile.LogTracing("Show NutDesk Main Gui", LogLvl.LOG_DEBUG, this);
                NotifyIcon.Visible = false;
            }

            // Begin auto-connecting if user indicated they wanted it. The connection runs on a background thread, so
            // this no longer blocks the form before it has painted.
            if (Properties.Settings.Default.NUT_AutoReconnect)
            {
                LogFile.LogTracing("Auto-connecting to UPS on startup.", LogLvl.LOG_NOTICE, this);
                UPS_Connect();
            }

            // An in-place update that failed and restored this version. Posted, so the message comes after the
            // window is up instead of holding it back.
            if (Program.UpdateFailedReason != null)
            {
                BeginInvoke(new Action(ShowUpdateFailure));
            }
        }

        /// <summary>
        /// Tells the user that the in-place update this start follows failed. Program.Startup has logged it already.
        /// </summary>
        private void ShowUpdateFailure()
        {
            ShowUpdateMessage(Updating.UpdateText.Format(Properties.Resources.Update_InstallFailed, Program.UpdateFailedReason),
                              MessageBoxIcon.Error);
        }

        private void NutDesk_FormClosing(object sender, FormClosingEventArgs e)
        {
            LogFile.LogTracing("Received FormClosing event. Reason: " + e.CloseReason.ToString(), LogLvl.LOG_NOTICE, this);

            if (e.CloseReason == CloseReason.UserClosing && (Properties.Settings.Default.CloseToTray == true & Properties.Settings.Default.MinimizeToTray == true))
            {
                LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
                UpdateIcon_NotifyIcon();
                LogFile.LogTracing("Minimize Main Gui To Notify Icon", LogLvl.LOG_DEBUG, this);
                WindowState = FormWindowState.Minimized;
                Visible = false;
                NotifyIcon.Visible = true;
                e.Cancel = true;
            }
            else
            {
                LogFile.LogTracing("Init Disconnecting Before Close NutDesk", LogLvl.LOG_DEBUG, this);
                Microsoft.Win32.SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
                UPSDisconnect();
            }
        }

        /// <summary>
        /// The window is really gone, and with it the application: Application.Run returns once the main form has
        /// closed. FormClosing cannot be used for this, because it is cancelled whenever the window only goes to
        /// the tray, and the notifications must keep running then.
        /// </summary>
        private void NutDesk_FormClosed(object sender, FormClosedEventArgs e)
        {
            // A download still running in the update window is abandoned cleanly rather than cut off by the exit.
            StopUpdateCheckTimer();
            UpdateView?.Close();

            // Cleared before disposing so that nothing still running on this thread can reach a disposed center.
            var center = notifications;
            notifications = null;

            if (center == null)
            {
                return;
            }

            LogFile.LogTracing("Releasing the notification service.", LogLvl.LOG_DEBUG, this);

            try
            {
                center.Dispose();
            }
            catch (Exception ex)
            {
                // The application is exiting; a notification problem must not turn that into a crash report.
                LogFile.LogTracing("Error while releasing the notification service.", LogLvl.LOG_WARNING, this);
                LogFile.LogException(ex, this);
            }
        }

        private void NutDesk_Resize(object sender, EventArgs e)
        {
            var form = (Form)sender;
            if (form.WindowState == FormWindowState.Minimized)
            {
                if (Properties.Settings.Default.MinimizeToTray == true)
                {
                    LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
                    UpdateIcon_NotifyIcon();
                    LogFile.LogTracing("Minimize Main Gui To Notify Icon", LogLvl.LOG_DEBUG, this);
                    WindowState = FormWindowState.Minimized;
                    Visible = false;
                    NotifyIcon.Visible = true;
                }
                if (NotifyIcon.Visible == false)
                {
                    Text = FormText;
                }
                else
                {
                    Text = ProgramName;
                }
            }
            else if (form.WindowState == FormWindowState.Maximized || form.WindowState == FormWindowState.Normal)
            {
                Text = ProgramName;
            }
        }

        private void NutDesk_Deactivate(object sender, EventArgs e)
        {
            if (NutDesk_Crashed)
            {
                Hide();
            }
            else
            {
                LogFile.LogTracing("Main Gui Lose Focus", LogLvl.LOG_DEBUG, this);
                HasFocus = false;
                int Tmp_App_Mode;
                if (!AppDarkMode)
                {
                    Tmp_App_Mode = (int)(AppIconIdx.WIN_DARK | AppIconIdx.IDX_OFFSET);
                }
                else
                {
                    Tmp_App_Mode = (int)AppIconIdx.IDX_OFFSET;
                }
                var TmpGuiIDX = ActualAppIconIdx | Tmp_App_Mode;
                Icon = GetIcon(TmpGuiIDX);
                LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
                UpdateIcon_NotifyIcon();
            }
        }

        #endregion

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "NUT_AutoReconnect")
            {
                LogFile.LogTracing("Handling OnPropertyChanged for " + e.PropertyName, LogLvl.LOG_DEBUG, this);

                // Push the new value into the live device. It used to be a snapshot taken when the device was created,
                // so toggling "Persist connection" had no effect until the next connection from scratch.
                if (UPS_Device != null)
                {
                    UPS_Device.SetAutoReconnect(Properties.Settings.Default.NUT_AutoReconnect);
                }

                UpdateMainMenuState();
            }
            else if (e.PropertyName == "LG_LogToFile")
            {
                // The log window's "open log file" button is only usable while file logging is on, and the
                // preferences dialog can turn it on with the window already open.
                LogView?.RefreshFileState();
            }
        }

        private void UpdateMainMenuState()
        {
            if (OldParams.NutDesk_Params.ParamsExist)
            {
                ManageOldPrefsToolStripMenuItem.Enabled = true;
                ManageOldPrefsToolStripMenuItem.ToolTipText = Properties.Resources.ManageOldPrefsToolstripMenuItem_Enabled_TooltipText;
            }
            else
            {
                ManageOldPrefsToolStripMenuItem.Enabled = false;
                ManageOldPrefsToolStripMenuItem.ToolTipText = Properties.Resources.ManageOldPrefsToolstripMenuItem_Disabled_TooltipText;
            }

            Menu_Persist.Checked = Properties.Settings.Default.NUT_AutoReconnect;
            Menu_UPS_Var.Enabled = UPS_Device?.IsConnected ?? false;

            // Disconnect stays available while connecting or retrying, so a connection to an unresponsive server
            // can always be cancelled.
            Menu_Disconnect.Enabled = (UPS_Device?.IsConnected ?? false) ||
                                      (UPS_Device?.IsReconnecting ?? false) ||
                                      (UPS_Device?.IsConnecting ?? false);
            Menu_Connect.Enabled = !Menu_Disconnect.Enabled;

            // The dashboard's menu copies this state rather than sharing it, so it has to be told.
            AppMenuModel?.Refresh();

            // The dashboard's header buttons stand in for these menu items, so they follow the same state -
            // and so does the badge, which must never read "not connected" next to an enabled Disconnect.
            RefreshStatusPill();
        }

        /// <summary>
        /// SystemEvents raises this on its own listener thread, so everything it does - touching controls, starting
        /// and stopping WinForms timers - has to be marshalled onto the UI thread first.
        /// </summary>
        private void SystemEvents_PowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<object, Microsoft.Win32.PowerModeChangedEventArgs>(
                        SystemEvents_PowerModeChanged), sender, e);
                }
                catch (Exception ex)
                {
                    LogFile.LogTracing("Unable to marshal PowerModeChanged onto the UI thread.", LogLvl.LOG_ERROR, this);
                    LogFile.LogException(ex, this);
                }
                return;
            }

            LogFile.LogTracing("PowerModeChangedEvent: " + Enum.GetName(typeof(Microsoft.Win32.PowerModes), e.Mode), LogLvl.LOG_NOTICE, this);
            switch (e.Mode)
            {
                // Note: Windows does not wait for applications to handle a Suspend event.
                case Microsoft.Win32.PowerModes.Suspend:
                    LogFile.LogTracing("Suspending NutDesk operations...", LogLvl.LOG_NOTICE, this, StrLog[(int)AppResxStr.STR_MAIN_GOTOSLEEP]);
                    UPSDisconnect();
                    break;

                case Microsoft.Win32.PowerModes.Resume:
                    // Unconditional: a device that is merely mid-reconnect is not IsConnected, and leaving it behind
                    // would strand an orphan still retrying against the pre-suspend socket.
                    if (UPS_Device != null)
                    {
                        LogFile.LogTracing("Discarding the pre-suspend UPS connection after system resume...", LogLvl.LOG_NOTICE, this);
                        UPSDisconnect();
                    }
                    if (Properties.Settings.Default.NUT_AutoReconnect)
                    {
                        LogFile.LogTracing("Reconnecting after system resume.", LogLvl.LOG_NOTICE, this, StrLog[(int)AppResxStr.STR_MAIN_EXITSLEEP]);
                        UPS_Connect();
                    }
                    break;
            }
        }

        private void UPS_Connect()
        {
            Nut_Parameter Nut_Config;
            LogFile.LogTracing("Client UPS_Connect subroutine beginning.", LogLvl.LOG_NOTICE, this);

            // A fresh installation still carries the placeholder host and UPS name. Connecting with those produces
            // a confusing protocol-level complaint, so say what is actually wrong instead.
            if (IsUsingPlaceholderSettings())
            {
                LogFile.LogTracing("Connection settings are still at their defaults; nothing to connect to.",
                                   LogLvl.LOG_WARNING, this, StrLog[(int)AppResxStr.STR_MAIN_UNKNOWN_UPS]);
                Event_Unknown_UPS();
                UpdateMainMenuState();
                return;
            }

            // Replacing the field without disposing the previous device would leave its WinForms timers running: an
            // invisible orphan that keeps polling and reconnecting to whatever server it was originally configured for.
            TearDownUPSDevice();

            Nut_Config = new Nut_Parameter(Properties.Settings.Default.NUT_ServerAddress,
                                           Properties.Settings.Default.NUT_ServerPort,
                                           Properties.Settings.Default.NUT_Username,
                                           Properties.Settings.Default.NUT_Password,
                                           Properties.Settings.Default.NUT_UPSName,
                                           Properties.Settings.Default.NUT_AutoReconnect);

            UPS_Device = new UPS_Device(Nut_Config, LogFile, Properties.Settings.Default.NUT_PollIntervalMsec, Properties.Settings.Default.CAL_FreqInNom);
            UPS_Device.EncounteredNUTException += HandleNUTException;

            // Returns immediately - the connection is made on a background thread and reported through events.
            UPS_Device.Connect_UPS();
            UpdateMainMenuState();
        }

        /// <summary>
        /// True while the connection settings are still the values shipped with the application.
        /// </summary>
        private bool IsUsingPlaceholderSettings()
        {
            try
            {
                var defaultHost = Convert.ToString(Properties.Settings.Default.Properties["NUT_ServerAddress"].DefaultValue);
                var defaultName = Convert.ToString(Properties.Settings.Default.Properties["NUT_UPSName"].DefaultValue);

                return string.IsNullOrWhiteSpace(Properties.Settings.Default.NUT_ServerAddress) ||
                       string.IsNullOrWhiteSpace(Properties.Settings.Default.NUT_UPSName) ||
                       string.Equals(Properties.Settings.Default.NUT_ServerAddress, defaultHost, StringComparison.Ordinal) ||
                       string.Equals(Properties.Settings.Default.NUT_UPSName, defaultName, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                LogFile.LogTracing("Unable to compare settings against their defaults.", LogLvl.LOG_DEBUG, this);
                return false;
            }
        }

        /// <summary>
        /// Release the current UPS device completely: unsubscribe, stop its timers and forget it.
        /// </summary>
        private void TearDownUPSDevice()
        {
            if (UPS_Device == null)
            {
                return;
            }

            LogFile.LogTracing("Tearing down the previous UPS device.", LogLvl.LOG_DEBUG, this);
            UPS_Device.EncounteredNUTException -= HandleNUTException;

            try
            {
                UPS_Device.Dispose();
            }
            catch (Exception ex)
            {
                LogFile.LogTracing("Error while disposing the previous UPS device.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);
            }

            UPS_Device = null;
        }

        /// <summary>
        /// Prepare the form to begin receiving data from a connected UPS.
        /// </summary>
        private void UPSReady(UPS_Device nutUps)
        {
            var upsConf = nutUps.Nut_Config;
            LogFile.LogTracing(upsConf.UPSName + " has indicated it's ready to start sending data.", LogLvl.LOG_DEBUG, this);

            var datas = UPS_Device.UPS_Datas;
            Lbl_VMfr.Text = datas.Mfr;
            Lbl_VName.Text = datas.Model;
            Lbl_VSerial.Text = datas.Serial;
            Lbl_VFirmware.Text = datas.Firmware;

            UpdateMainMenuState();
            PushReadingsToDashboard();
            UpdateIcon_NotifyIcon();
            LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
            UpdateNotifyIconStr?.Invoke("Connected", null);
            LogFile.LogTracing("Connection to Nut Host Established", LogLvl.LOG_NOTICE, this,
                               string.Format(StrLog[(int)AppResxStr.STR_LOG_CONNECTED],
                                             upsConf.Host, upsConf.Port));
        }

        private void ConnectionError(UPS_Device sender, Exception ex)
        {
            if (!UPS_Device.IsReconnecting)
            {
                LogFile.LogTracing(string.Format("Something went wrong connecting to UPS {0}. IsConnected: {1}, IsLoggedIn: {2}",
                                sender.Name, sender.IsConnected, sender.IsLoggedIn), LogLvl.LOG_ERROR, this,
                                string.Format(StrLog[(int)AppResxStr.STR_LOG_CON_FAILED], sender.Nut_Config.Host, sender.Nut_Config.Port,
                                                ex.Message));
            }

            // Cheap and idempotent: without it a failed attempt can leave Connect greyed out.
            UpdateMainMenuState();
        }

        /// <summary>
        /// Prepare application for and initiate disconnecting from the UPS.
        /// </summary>
        private void UPSDisconnect()
        {
            LogFile.LogTracing("Running Client disconnect subroutine.", LogLvl.LOG_DEBUG, this);

            if (UPS_Device == null)
            {
                LogFile.LogTracing("Nothing to disconnect - no UPS device is active.", LogLvl.LOG_DEBUG, this);
                return;
            }

            // cancelReconnect:=True - an explicit disconnect must also abandon any reconnection loop.
            UPS_Device.Disconnect(true);
            TearDownUPSDevice();
            UpdateMainMenuState();
        }

        private void UPS_Lostconnect()
        {
            LogFile.LogTracing("UPS reports lost (broken) connection.", LogLvl.LOG_ERROR, this,
                    string.Format(StrLog[(int)AppResxStr.STR_MAIN_LOSTCONNECT], UPS_Device.Nut_Config.Host, UPS_Device.Nut_Config.Port));

            ReInitDisplayValues();
            if (UPS_Device.Nut_Config.AutoReconnect)
            {
                ActualAppIconIdx = (int)AppIconIdx.IDX_ICO_RETRY;

                // The device schedules the retry before announcing the loss, so by now UPS_Reconnecting has already
                // shown the attempt number and the delay. Do not overwrite that with the generic wording.
                if (!UPS_Device.IsReconnecting)
                {
                    var Message = string.Format(StrLog[(int)AppResxStr.STR_MAIN_RECONNECT]);
                    LogFile.LogTracing("UPS reports it lost connection and is retrying.", LogLvl.LOG_WARNING, this, Message);
                    UpdateNotifyIconStr?.Invoke("Retry", Message);
                }
            }
            else
            {

                ActualAppIconIdx = (int)AppIconIdx.IDX_ICO_OFFLINE;
                UpdateNotifyIconStr?.Invoke("Lost Connect", null);
            }

            UpdateIcon_NotifyIcon();
            LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
            UpdateBatteryState?.Invoke("Lost Connect");
            UpdateMainMenuState();
        }

        /// <summary>
        /// Another reconnection attempt has been scheduled. Reported separately from Lost_Connect so the user can
        /// tell "still trying" apart from "gave up" - previously both looked identical in the log and the tray.
        /// </summary>
        private void UPS_Reconnecting(UPS_Device sender, int attemptNumber, int delayMs)
        {
            // Kept language-neutral around the already localized "reconnecting" string.
            var Message = string.Format("{0} (#{1} - {2}s)", StrLog[(int)AppResxStr.STR_MAIN_RECONNECT],
                                        attemptNumber, Convert.ToInt32(Math.Round(delayMs / 1000.0)));

            LogFile.LogTracing(string.Format("Reconnection attempt {0} scheduled in {1} ms.", attemptNumber, delayMs),
                               LogLvl.LOG_WARNING, this, Message);

            ActualAppIconIdx = (int)AppIconIdx.IDX_ICO_RETRY;
            UpdateNotifyIconStr?.Invoke("Retry", Message);
            UpdateIcon_NotifyIcon();
            UpdateMainMenuState();
        }

        /// <summary>
        /// The device has stopped retrying, either because automatic reconnection is off or because retrying cannot
        /// succeed. Make that explicit rather than leaving a "retrying" icon on screen forever.
        /// </summary>
        private void UPS_ReconnectAbandoned(UPS_Device sender, string reason)
        {
            LogFile.LogTracing("Reconnection abandoned: " + reason, LogLvl.LOG_WARNING, this, reason);

            ActualAppIconIdx = (int)AppIconIdx.IDX_ICO_OFFLINE;
            UpdateNotifyIconStr?.Invoke("Lost Connect", null);
            UpdateIcon_NotifyIcon();
            UpdateMainMenuState();
        }

        /// <summary>
        /// Perform final actions to wrap up a disconnected UPS.
        /// </summary>
        private void UPSDisconnectedEvent()
        {
            ReInitDisplayValues();
            UpdateMainMenuState();
            ActualAppIconIdx = (int)AppIconIdx.IDX_ICO_OFFLINE;
            LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
            UpdateIcon_NotifyIcon();
            UpdateNotifyIconStr?.Invoke("Deconnected", null);
            UpdateBatteryState?.Invoke("Deconnected");

            LogFile.LogTracing("Disconnected from Nut Host", LogLvl.LOG_NOTICE, this, StrLog[(int)AppResxStr.STR_LOG_LOGOFF]);
        }

        private void Menu_Quit_Click_1(object sender, EventArgs e)
        {
            LogFile.LogTracing("Close NutDesk From Menu Quit", LogLvl.LOG_DEBUG, this);
            Application.Exit();
        }

        private void Menu_Sys_Exit_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Close NutDesk From Systray", LogLvl.LOG_DEBUG, this);
            Application.Exit();
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                LogFile.LogTracing("Restore Main Gui On Mouse Click Notify Icon", LogLvl.LOG_DEBUG, this);
                RestoreFromTray();
            }
        }

        /// <summary>
        /// Brings the window back from the tray. Shared by the tray icon and the notifications, so that opening
        /// the application from a toast leaves the tray icon and the window in the same state a click would.
        /// </summary>
        private void RestoreFromTray()
        {
            Visible = true;
            NotifyIcon.Visible = false;
            WindowState = FormWindowState.Normal;
        }

        #region "Notifications"

        private void CreateNotificationCenter()
        {
            if (notifications != null)
            {
                return;
            }

            try
            {
                notifications = new Notifications.NotificationCenter(LogFile, ShowFromNotification, ShowBalloon);
                LogFile.LogTracing("Notification service started.", LogLvl.LOG_DEBUG, this);
            }
            catch (Exception ex)
            {
                // Notifications are an extra: without them the client still monitors the UPS and still shuts the
                // computer down, which is what matters.
                LogFile.LogTracing("Unable to start the notification service; notifications are disabled.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);
                notifications = null;
                return;
            }

            // A device only reaches the center through the UPS_Device setter, and one assigned before this point
            // went through it while the center did not exist yet.
            FollowDeviceNotifications(UPS_Device, true);
        }

        /// <summary>
        /// Starts or stops the center following a device. A failure here must not stop the shell from wiring or
        /// releasing that device, so it is logged and forgotten.
        /// </summary>
        private void FollowDeviceNotifications(UPS_Device device, bool follow)
        {
            var center = notifications;
            if (center == null || device == null)
            {
                return;
            }

            try
            {
                if (follow)
                {
                    center.Attach(device);
                }
                else
                {
                    center.Detach(device);
                }
            }
            catch (Exception ex)
            {
                LogFile.LogTracing("Unable to " + (follow ? "attach" : "detach") + " the notification service to the UPS device.",
                                   LogLvl.LOG_WARNING, this);
                LogFile.LogException(ex, this);
            }
        }

        /// <summary>
        /// Reports an event the shell detects itself. Guarded because the callers are the shutdown sequence and the
        /// update check: a notification that fails must never be the reason a computer on battery stays up.
        /// </summary>
        private void Notify(Action<Notifications.NotificationCenter> report, string what)
        {
            var center = notifications;
            if (center == null)
            {
                return;
            }

            try
            {
                report(center);
            }
            catch (Exception ex)
            {
                LogFile.LogTracing("Unable to report " + what + " to the notification service.", LogLvl.LOG_WARNING, this);
                LogFile.LogException(ex, this);
            }
        }

        /// <summary>
        /// Opens the application from a notification the user clicked.
        /// </summary>
        private void ShowFromNotification()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            // The center promises the UI thread; this only keeps a broken promise from touching the window from
            // another one.
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(ShowFromNotification));
                }
                catch
                {
                    // The window is going away; there is nothing left to show.
                }
                return;
            }

            LogFile.LogTracing("Restore Main Gui From Notification", LogLvl.LOG_DEBUG, this);

            // Only when there is something to restore: a window the user has open and maximized would otherwise
            // be put back to its normal size just because a toast was clicked.
            if (!Visible || WindowState == FormWindowState.Minimized)
            {
                RestoreFromTray();
            }

            Activate();
        }

        /// <summary>
        /// The center's fallback where Windows toast notifications cannot be shown.
        /// </summary>
        private void ShowBalloon(string title, string text, bool isCritical)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string, string, bool>(ShowBalloon), title, text, isCritical);
                }
                catch
                {
                    // The window is going away; the balloon is not worth chasing.
                }
                return;
            }

            // Windows anchors a balloon to its tray icon, and this one is hidden whenever the window is on screen.
            if (!NotifyIcon.Visible)
            {
                LogFile.LogTracing("Balloon notification skipped, the tray icon is not visible: " + title, LogLvl.LOG_DEBUG, this);
                return;
            }

            try
            {
                NotifyIcon.ShowBalloonTip(10000, title ?? string.Empty, text ?? string.Empty,
                                          isCritical ? ToolTipIcon.Warning : ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                // A balloon depends on the shell and can fail for reasons that have nothing to do with the UPS.
                LogFile.LogTracing("Unable to show a balloon notification.", LogLvl.LOG_WARNING, this);
                LogFile.LogException(ex, this);
            }
        }

        #endregion

        private void Event_UpdateNotifyIconStr(string Reason = null, string Message = null)
        {
            string ShowVersion = ShortProgramVersion;
            string NotifyStr = ProgramName + " - " + ShowVersion + Environment.NewLine;
            string FormText = ProgramName;
            switch (Reason)
            {
                // VB's Select Case on a String matches "" against Case Nothing as well, so both are listed here.
                case null:
                case "":
                    if ((UPS_Device == null) || !UPS_Device.IsConnected)
                    {
                        NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_NOTCONN];
                        FormText += " - " + StrLog[(int)AppResxStr.STR_MAIN_NOTCONN];
                    }
                    break;

                case "Retry":
                    NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_RECONNECT] + Environment.NewLine;
                    NotifyStr += Message;
                    FormText += " - Bat: " + UPS_BattCh + "% - " + StrLog[(int)AppResxStr.STR_MAIN_RECONNECT] + " - " + Message;
                    break;

                case "Connected":
                    NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_CONN];
                    FormText += " - Bat: " + UPS_BattCh + "% - " + StrLog[(int)AppResxStr.STR_MAIN_CONN];
                    break;

                case "Deconnected":
                    NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_NOTCONN];
                    FormText += " - " + StrLog[(int)AppResxStr.STR_MAIN_NOTCONN];
                    break;

                case "Unknown UPS":
                    NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_UNKNOWN_UPS];
                    FormText += " - " + StrLog[(int)AppResxStr.STR_MAIN_UNKNOWN_UPS];
                    break;

                case "Lost Connect":
                    if (UPS_Device != null)
                    {
                        NotifyStr += string.Format(StrLog[(int)AppResxStr.STR_MAIN_LOSTCONNECT], UPS_Device.Nut_Config.Host, UPS_Device.Nut_Config.Port);
                        FormText += " - " + string.Format(StrLog[(int)AppResxStr.STR_MAIN_LOSTCONNECT], UPS_Device.Nut_Config.Host, UPS_Device.Nut_Config.Port);
                    }
                    else
                    {
                        NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_NOTCONN];
                        FormText += " - " + StrLog[(int)AppResxStr.STR_MAIN_NOTCONN];
                    }
                    break;

                case "Update Data":
                    if (UPS_Device == null || UPS_Device.UPS_Datas == null)
                    {
                        break;
                    }

                    FormText += " - Bat: " + UPS_BattCh + "% - " + StrLog[(int)AppResxStr.STR_MAIN_CONN] + " - ";
                    NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_CONN] + Environment.NewLine;
                    if (UPS_Device.UPS_Datas.UPS_Value.UPS_Status.HasFlag(UPS_States.OL))
                    {
                        NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_OL] + Environment.NewLine;
                        FormText += StrLog[(int)AppResxStr.STR_MAIN_OL] + " - ";
                    }
                    else
                    {
                        NotifyStr += string.Format(StrLog[(int)AppResxStr.STR_MAIN_OB], UPS_Device.UPS_Datas.UPS_Value.Batt_Charge) + Environment.NewLine;
                        FormText += string.Format(StrLog[(int)AppResxStr.STR_MAIN_OB], UPS_Device.UPS_Datas.UPS_Value.Batt_Charge) + " - ";
                    }
                    var battCharge = UPS_Device.UPS_Datas.UPS_Value.Batt_Charge;
                    if (battCharge >= 0 && battCharge <= 40)
                    {
                        NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_LOWBAT];
                        FormText += StrLog[(int)AppResxStr.STR_MAIN_LOWBAT];
                    }
                    else if (battCharge >= 41 && battCharge <= 100)
                    {
                        NotifyStr += StrLog[(int)AppResxStr.STR_MAIN_BATOK];
                        FormText += StrLog[(int)AppResxStr.STR_MAIN_BATOK];
                    }
                    break;
            }
            if (NotifyStr.Length > 63)
            {
                NotifyStr = NotifyStr.Substring(0, 60) + "...";
            }
            NotifyIcon.Text = NotifyStr;
            if (WindowState == FormWindowState.Minimized & NotifyIcon.Visible == false)
            {
                Text = FormText;
            }
            else
            {
                Text = ProgramName;
            }
            this.FormText = FormText;

            UpdateStatusPill(Reason, Message);
            LogFile.LogTracing("NotifyIcon Text => " + Environment.NewLine + NotifyStr, LogLvl.LOG_DEBUG, this);
        }

        /// <summary>
        /// Drive the header's status badge from the same reasons that build the tray tooltip, so the two can never
        /// disagree.
        /// </summary>
        private void UpdateStatusPill(string Reason, string Message)
        {
            if (DashboardModel == null)
            {
                return;
            }

            switch (Reason)
            {
                case "Connected":
                    SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Online,
                                       StrLog[(int)AppResxStr.STR_MAIN_CONN]);
                    break;

                case "Retry":
                    SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Connecting,
                                       Message ?? StrLog[(int)AppResxStr.STR_MAIN_RECONNECT]);
                    break;

                case "Lost Connect":
                    SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Alert,
                                       StrLog[(int)AppResxStr.STR_MAIN_NOTCONN]);
                    break;

                case "Unknown UPS":
                    SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Alert,
                                       StrLog[(int)AppResxStr.STR_MAIN_UNKNOWN_UPS]);
                    break;

                case "Update Data":
                    if (UPS_Device != null && UPS_Device.UPS_Datas != null &&
                        UPS_Device.UPS_Datas.UPS_Value.UPS_Status.HasFlag(UPS_States.OL))
                    {
                        SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Online,
                                           StrLog[(int)AppResxStr.STR_MAIN_OL]);
                    }
                    else
                    {
                        SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.OnBattery,
                                           string.Format(StrLog[(int)AppResxStr.STR_MAIN_OB], UPS_BattCh));
                    }
                    break;

                default:
                    SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Offline,
                                       StrLog[(int)AppResxStr.STR_MAIN_NOTCONN]);
                    break;
            }

            RefreshDashboardChrome();
        }

        /// <summary>
        /// Derives the status badge from the device's own state instead of from an event reason, for the
        /// callers that have none to offer - a theme change, a menu state refresh, a first connection
        /// attempt. Those used to leave the badge contradicting the buttons next to it.
        /// </summary>
        private void RefreshStatusPill()
        {
            if (DashboardModel == null)
            {
                return;
            }

            var device = UPS_Device;

            if (device != null && device.IsConnected)
            {
                if (device.UPS_Datas != null && device.UPS_Datas.UPS_Value.UPS_Status.HasFlag(UPS_States.OL))
                {
                    SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Online,
                                       StrLog[(int)AppResxStr.STR_MAIN_OL]);
                }
                else
                {
                    SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.OnBattery,
                                       string.Format(StrLog[(int)AppResxStr.STR_MAIN_OB], UPS_BattCh));
                }
            }
            else if (device != null && device.IsReconnecting)
            {
                SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Connecting,
                                   StrLog[(int)AppResxStr.STR_MAIN_RECONNECT]);
            }
            else if (device != null && device.IsConnecting)
            {
                SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Connecting,
                                   StrLog[(int)AppResxStr.STR_MAIN_CONNECTING]);
            }
            else
            {
                SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind.Offline,
                                   StrLog[(int)AppResxStr.STR_MAIN_NOTCONN]);
            }

            RefreshDashboardChrome();
        }

        private void SetDashboardStatus(Wpf.DashboardViewModel.ConnectionKind kind, string text)
        {
            DashboardModel.ConnectionState = kind;
            DashboardModel.ConnectionText = text ?? string.Empty;
        }

        private void Event_UpdateBatteryState(string Reason = null)
        {
            string Status = "Unknown";
            switch (Reason)
            {
                // VB's Select Case on a String matches "" against Case Nothing as well, so both are listed here.
                case null:
                case "":
                case "Deconnected":
                case "Lost Connect":
                    if ((UPS_Device != null) && !UPS_Device.IsConnected)
                    {
                        PBox_Battery_State.Image = null;
                    }
                    Status = "Unknown";
                    break;

                case "Update Data":
                    if (UPS_BattCh == 100)
                    {
                        PBox_Battery_State.Image = (Image)Properties.Resources.Battery_Charged;
                        Status = "Charged";
                    }
                    else
                    {
                        if (UPS_Status.Trim().StartsWith("OL") || StrReverse(UPS_Status.Trim()).StartsWith("LO"))
                        {
                            PBox_Battery_State.Image = (Image)Properties.Resources.Battery_Charging;
                            Status = "Charging";
                        }
                        else
                        {
                            PBox_Battery_State.Image = (Image)Properties.Resources.Battery_Discharging;
                            Status = "Discharging";
                        }
                    }
                    break;
            }
            LogFile.LogTracing("Battery Status => " + Status, LogLvl.LOG_DEBUG, this);
        }

        private void HandleNUTException(UPS_Device sender, NutException ex)
        {
            if (ex.LastTransaction.ResponseType == NUTResponse.UNKNOWNUPS)
            {
                Event_Unknown_UPS();
            }

            LogFile.LogTracing("NUT protocol error encoutnered:", LogLvl.LOG_NOTICE, sender);
            LogFile.LogException(ex, this);
            UpdateMainMenuState();
        }

        public void Event_Unknown_UPS() // Handles UPS_Device.Unknown_UPS
        {
            ActualAppIconIdx = (int)AppIconIdx.IDX_ICO_OFFLINE;
            LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
            UpdateIcon_NotifyIcon();
            UpdateNotifyIconStr?.Invoke("Unknown UPS", null);
            LogFile.LogTracing("Unknow UPS Name", LogLvl.LOG_ERROR, this, StrLog[(int)AppResxStr.STR_MAIN_UNKNOWN_UPS]);
            Menu_UPS_Var.Enabled = false;
        }

        private void Menu_About_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Open About Gui From Menu", LogLvl.LOG_DEBUG, this);
            About_Gui.Instance.Activate();
            About_Gui.Instance.Visible = true;
            HasFocus = false;
        }

        private void Menu_Sys_About_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Open About Gui From Systray", LogLvl.LOG_DEBUG, this);
            About_Gui.Instance.Activate();
            About_Gui.Instance.Visible = true;
            HasFocus = false;
        }

        private void Update_UPS_Data()
        {
            LogFile.LogTracing("Updating UPS data for Form.", LogLvl.LOG_DEBUG, this);

            // Capture the device once: a stop condition detected below disconnects and clears the field part way
            // through this routine, and everything after that point would otherwise dereference Nothing.
            var device = UPS_Device;
            if (device == null || device.UPS_Datas == null)
            {
                return;
            }

            var values = device.UPS_Datas.UPS_Value;
            UPS_BattCh = values.Batt_Charge;
            UPS_BattV = values.Batt_Voltage;
            UPS_BattRuntime = values.Batt_Runtime;
            UPS_BattCapacity = values.Batt_Capacity;
            UPS_InputF = values.Power_Frequency;
            UPS_InputV = values.Input_Voltage;
            UPS_OutputV = values.Output_Voltage;
            UPS_Load = values.Load;
            UPS_Status = values.UPS_Status.ToString();
            UPS_OutPower = values.Output_Power;

            if (values.UPS_Status.HasFlag(UPS_States.OL))
            {
                Lbl_VOL.BackColor = Theme.Success;
                Lbl_VOB.BackColor = Theme.Neutral;
                ActualAppIconIdx = (int)AppIconIdx.IDX_OL;
            }
            else if (values.UPS_Status.HasFlag(UPS_States.OB))
            {
                // Mains lost is the alarming half of this pair; running on battery is merely a warning.
                Lbl_VOL.BackColor = Theme.Danger;
                Lbl_VOB.BackColor = Theme.Warning;
                ActualAppIconIdx = 0;

                if (values.Batt_Charge == -1 && values.Batt_Runtime == -1)
                {
                    LogFile.LogTracing("Battery properties unavailable, unable to validate shutdown conditions.", LogLvl.LOG_WARNING, this);
                }
                else if (!ShutdownStatus)
                {
                    if ((values.Batt_Charge != -1 && values.Batt_Charge <= Properties.Settings.Default.PW_BattChrgFloor) ||
                        (values.Batt_Runtime != -1 && values.Batt_Runtime <= Properties.Settings.Default.PW_RuntimeFloor))
                    {
                        LogFile.LogTracing("UPS battery has dropped below stop condition limits.",
                                               LogLvl.LOG_NOTICE, this, StrLog[(int)AppResxStr.STR_LOG_SHUT_START]);
                        Shutdown_Event(false);
                    }
                    else
                    {
                        LogFile.LogTracing(string.Format("UPS charge ({0}%) or Runtime ({1}) have not met shutdown conditions {2} or {3}.",
                            values.Batt_Charge, values.Batt_Runtime, Properties.Settings.Default.PW_BattChrgFloor, Properties.Settings.Default.PW_RuntimeFloor),
                            LogLvl.LOG_DEBUG, this);
                    }
                }
            }

            if (values.UPS_Status.HasFlag(UPS_States.OVER))
            {
                Lbl_VOLoad.BackColor = Theme.Danger;
            }
            else
            {
                Lbl_VOLoad.BackColor = Theme.Neutral;
            }

            LogFile.LogTracing("Updating battery icons based on charge percent: " + UPS_BattCh + "%", LogLvl.LOG_DEBUG, this);

            // Contiguous bands: the charge is a double, so the old integer-looking ranges (76-100, 51-75, ...) left
            // gaps such as 75.5 or 10.5 that set no level at all. An unknown charge (-1) is not an alarm: it is drawn
            // full on mains and half-full on battery, and the label stays neutral.
            var onMains = (ActualAppIconIdx & (int)AppIconIdx.IDX_OL) != 0;

            if (UPS_BattCh < 0)
            {
                Lbl_VBL.BackColor = Theme.Neutral;
                ActualAppIconIdx |= (int)(onMains ? AppIconIdx.IDX_BATT_100 : AppIconIdx.IDX_BATT_50);
            }
            else if (UPS_BattCh > 75)
            {
                Lbl_VBL.BackColor = Theme.Neutral;
                ActualAppIconIdx |= (int)AppIconIdx.IDX_BATT_100;
            }
            else if (UPS_BattCh > 50)
            {
                Lbl_VBL.BackColor = Theme.Neutral;
                ActualAppIconIdx |= (int)AppIconIdx.IDX_BATT_75;
            }
            else if (UPS_BattCh >= 40)
            {
                Lbl_VBL.BackColor = Theme.Neutral;
                ActualAppIconIdx |= (int)AppIconIdx.IDX_BATT_50;
            }
            else if (UPS_BattCh > 25)
            {
                Lbl_VBL.BackColor = Theme.Danger;
                ActualAppIconIdx |= (int)AppIconIdx.IDX_BATT_50;
            }
            else if (UPS_BattCh > 10)
            {
                Lbl_VBL.BackColor = Theme.Danger;
                ActualAppIconIdx |= (int)AppIconIdx.IDX_BATT_25;
            }
            else
            {
                Lbl_VBL.BackColor = Theme.Danger;
                ActualAppIconIdx |= (int)AppIconIdx.IDX_BATT_0;
            }

            // Calculate and display estimated remaining time on battery.
            if (UPS_BattRuntime >= 0 && UPS_BattRuntime <= 86400)
            {
                TimeSpan iSpan = TimeSpan.FromSeconds(UPS_BattRuntime);
                LogFile.LogTracing("Calculated estimated remaining battery time: " + iSpan.ToString(), LogLvl.LOG_DEBUG, this);

                // Format the TimeSpan using a standard format (g = 0:00:00)
                // https://docs.microsoft.com/en-us/dotnet/api/system.timespan.tostring
                Lbl_VRTime.Text = iSpan.ToString("g");
                // Lbl_VRTime.Text = iSpan.Hours.ToString.PadLeft(2, "0"c) & ":" &
                // iSpan.Minutes.ToString.PadLeft(2, "0"c) & ":" &
                // iSpan.Seconds.ToString.PadLeft(2, "0"c)
                // End If
            }
            else
            {
                Lbl_VRTime.Text = Properties.Resources.VariableUnavailable;
            }

            LogFile.LogTracing("Update Dial", LogLvl.LOG_DEBUG, this);
            AG_InV.Value1 = (float)UPS_InputV;
            AG_InF.Value1 = (float)UPS_InputF;
            AG_OutV.Value1 = (float)UPS_OutputV;
            AG_BattCh.Value1 = (float)UPS_BattCh;
            AG_Load.Value1 = (float)UPS_Load;
            AG_Load.Value2 = (float)UPS_OutPower;
            AG_BattV.Value1 = (float)UPS_BattV;
            LogFile.LogTracing("Update Icon", LogLvl.LOG_DEBUG, this);
            UpdateIcon_NotifyIcon();
            UpdateNotifyIconStr?.Invoke("Update Data", null);
            UpdateBatteryState?.Invoke("Update Data");

            PushReadingsToDashboard();
        }

        private void Menu_Disconnect_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Disconnect from menu", LogLvl.LOG_DEBUG, this);
            UPSDisconnect();
        }

        private void ReInitDisplayValues()
        {
            LogFile.LogTracing("Initializing all display values and configurations.", LogLvl.LOG_DEBUG, this);
            UPS_Mfr = "";
            UPS_Model = "";
            UPS_Serial = "";
            UPS_Firmware = "";
            Lbl_VOL.BackColor = Theme.Neutral;
            Lbl_VOB.BackColor = Theme.Neutral;
            Lbl_VOLoad.BackColor = Theme.Neutral;
            Lbl_VBL.BackColor = Theme.Neutral;
            Lbl_VRTime.Text = "";
            Lbl_VMfr.Text = UPS_Mfr;
            Lbl_VName.Text = UPS_Model;
            Lbl_VSerial.Text = UPS_Serial;
            Lbl_VFirmware.Text = UPS_Firmware;
            AG_BattCh.Value1 = 0;
            AG_Load.Value1 = 0;
            AG_Load.Value2 = 0;

            AG_InV.Value1 = Properties.Settings.Default.CAL_VoltInMin;

            if ((AG_InV.MaxValue != Properties.Settings.Default.CAL_VoltInMax) || (AG_InV.MinValue != Properties.Settings.Default.CAL_VoltInMin))
            {
                LogFile.LogTracing("Parameter Dial Input Voltage Need to be Updated", LogLvl.LOG_DEBUG, this);
                AG_InV.MaxValue = Properties.Settings.Default.CAL_VoltInMax;
                AG_InV.MinValue = Properties.Settings.Default.CAL_VoltInMin;
                AG_InV.ScaleLinesMajorStepValue = Convert.ToInt32((AG_InV.MaxValue - AG_InV.MinValue) / 5.0);
                LogFile.LogTracing("Parameter Dial Input Voltage Updated", LogLvl.LOG_DEBUG, this);
            }

            AG_InF.Value1 = Properties.Settings.Default.CAL_FreqInMin;

            if ((AG_InF.MaxValue != Properties.Settings.Default.CAL_FreqInMax) || (AG_InF.MinValue != Properties.Settings.Default.CAL_FreqInMin))
            {
                LogFile.LogTracing("Parameter Dial Input Frequency Need to be Updated", LogLvl.LOG_DEBUG, this);
                AG_InF.MaxValue = Properties.Settings.Default.CAL_FreqInMax;
                AG_InF.MinValue = Properties.Settings.Default.CAL_FreqInMin;
                AG_InF.ScaleLinesMajorStepValue = Convert.ToInt32((AG_InF.MaxValue - AG_InF.MinValue) / 5.0);
                LogFile.LogTracing("Parameter Dial Input Frequency Updated", LogLvl.LOG_DEBUG, this);
            }

            AG_OutV.Value1 = Properties.Settings.Default.CAL_VoltOutMin;

            if ((AG_OutV.MaxValue != Properties.Settings.Default.CAL_VoltOutMax) || (AG_OutV.MinValue != Properties.Settings.Default.CAL_VoltOutMin))
            {
                LogFile.LogTracing("Parameter Dial Output Voltage Need to be Updated", LogLvl.LOG_DEBUG, this);
                AG_OutV.MaxValue = Properties.Settings.Default.CAL_VoltOutMax;
                AG_OutV.MinValue = Properties.Settings.Default.CAL_VoltOutMin;
                AG_OutV.ScaleLinesMajorStepValue = Convert.ToInt32((AG_OutV.MaxValue - AG_OutV.MinValue) / 5.0);
                LogFile.LogTracing("Parameter Dial Output Voltage Updated", LogLvl.LOG_DEBUG, this);
            }

            AG_BattV.Value1 = Properties.Settings.Default.CAL_BattVMin;

            if ((AG_BattV.MaxValue != Properties.Settings.Default.CAL_BattVMax) || (AG_BattV.MinValue != Properties.Settings.Default.CAL_BattVMin))
            {
                LogFile.LogTracing("Parameter Dial Voltage Battery Need to be Updated", LogLvl.LOG_DEBUG, this);
                AG_BattV.MaxValue = Properties.Settings.Default.CAL_BattVMax;
                AG_BattV.MinValue = Properties.Settings.Default.CAL_BattVMin;
                AG_BattV.ScaleLinesMajorStepValue = Convert.ToInt32((AG_BattV.MaxValue - AG_BattV.MinValue) / 5.0);
                LogFile.LogTracing("Parameter Dial Voltage Battery Updated", LogLvl.LOG_DEBUG, this);
            }

            // The ranges above may have just changed; the dashboard's scales follow them.
            PushRangesToDashboard();
            PushReadingsToDashboard();

            // Saving preferences while disconnected reaches here and nowhere else, so without this the header
            // subtitle kept showing the previous server address and UPS name until the next connection.
            RefreshDashboardChrome();
        }

        private void Menu_Reconnect_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Force Reconnect from menu", LogLvl.LOG_DEBUG, this);
            UPSDisconnect();

            // Connect_UPS now decides about retrying from the live NUT_AutoReconnect setting, so a manual connect
            // benefits from the reconnection loop exactly like the automatic one does.
            UPS_Connect();
        }

        private void OpenPrefsForm()
        {
            LogFile.LogTracing("Opening Prefs form...", LogLvl.LOG_NOTICE, this);

            try
            {
                var prefs = new Wpf.PreferencesWindow(Theme.IsDark);

                // Modal, as Pref_Gui was: the dialog edits the connection settings the shell is using, and the
                // shell reacts to a save through SavedPreferences. The WinForms owner handle is what makes a WPF
                // ShowDialog disable this form and centre on it.
                new System.Windows.Interop.WindowInteropHelper(prefs).Owner = Handle;
                prefs.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                prefs.ShowDialog();
            }
            catch (Exception ex)
            {
                // A menu action must not be able to take the application down.
                LogFile.LogTracing("Unable to open the preferences window.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);
            }
        }

        /// <summary>
        /// Reset the UI by cycling the connection if already connected and reinitialize display values.
        /// </summary>
        public void ResetUIState()
        {
            LogFile.LogTracing("Beginning ResetUIState subroutine.", LogLvl.LOG_DEBUG, this);
            var wasActive = false;

            // Any live device - connected OR still working through its reconnection loop - is holding the previous
            // server settings. Gating this on IsConnected alone meant that correcting the server address while the
            // client was retrying did nothing: it went on retrying the old address forever.
            if (UPS_Device != null)
            {
                wasActive = true;
                UPSDisconnect();
            }

            ReInitDisplayValues();

            // Only reconnect if a connection (or a reconnection loop) was actually running: an explicit
            // disconnect by the user must stay disconnected.
            if (wasActive)
            {
                UPS_Connect();
            }
        }

        private void UpdateIcon_NotifyIcon()
        {
            int Tmp_Win_Mode;
            int Tmp_App_Mode;
            if (ActualAppIconIdx != LastAppIconIdx)
            {
                LogFile.LogTracing("Status Icon Changed", LogLvl.LOG_DEBUG, this);
                if (WinDarkMode)
                {
                    Tmp_Win_Mode = (int)(AppIconIdx.WIN_DARK | AppIconIdx.IDX_OFFSET);
                }
                else
                {
                    Tmp_Win_Mode = (int)AppIconIdx.IDX_OFFSET;
                }
                if (AppDarkMode)
                {
                    Tmp_App_Mode = (int)(AppIconIdx.WIN_DARK | AppIconIdx.IDX_OFFSET);
                }
                else
                {
                    Tmp_App_Mode = (int)AppIconIdx.IDX_OFFSET;
                }
                var TmpGuiIDX = ActualAppIconIdx | Tmp_App_Mode;
                if (!HasFocus)
                {
                    TmpGuiIDX = TmpGuiIDX | (int)AppIconIdx.WIN_DARK;
                }
                var TmpTrayIDX = ActualAppIconIdx | Tmp_Win_Mode;
                LogFile.LogTracing("New Icon Value For Systray : " + TmpTrayIDX.ToString(), LogLvl.LOG_DEBUG, this);
                LogFile.LogTracing("New Icon Value For Gui : " + TmpGuiIDX.ToString(), LogLvl.LOG_DEBUG, this);
                NotifyIcon.Icon = GetIcon(TmpTrayIDX, SystemInformation.SmallIconSize);
                Icon = GetIcon(TmpGuiIDX);
                LastAppIconIdx = ActualAppIconIdx;
            }
        }

        // The 24 status icons, keyed by the AppIconIdx combination that names them: 5 charge levels x on mains or on
        // battery, plus offline and retry, each in a light (glyph for a light background) and a dark (+WIN_DARK) variant.
        private static readonly Dictionary<int, Func<System.Drawing.Icon>> StatusIcons = new Dictionary<int, Func<System.Drawing.Icon>>
        {
            { 1025, () => Properties.Resources._1025 }, { 1026, () => Properties.Resources._1026 },
            { 1028, () => Properties.Resources._1028 }, { 1032, () => Properties.Resources._1032 },
            { 1040, () => Properties.Resources._1040 }, { 1057, () => Properties.Resources._1057 },
            { 1058, () => Properties.Resources._1058 }, { 1060, () => Properties.Resources._1060 },
            { 1064, () => Properties.Resources._1064 }, { 1072, () => Properties.Resources._1072 },
            { 1089, () => Properties.Resources._1089 }, { 1090, () => Properties.Resources._1090 },
            { 1092, () => Properties.Resources._1092 }, { 1096, () => Properties.Resources._1096 },
            { 1104, () => Properties.Resources._1104 }, { 1121, () => Properties.Resources._1121 },
            { 1122, () => Properties.Resources._1122 }, { 1124, () => Properties.Resources._1124 },
            { 1128, () => Properties.Resources._1128 }, { 1136, () => Properties.Resources._1136 },
            { 1152, () => Properties.Resources._1152 }, { 1216, () => Properties.Resources._1216 },
            { 1280, () => Properties.Resources._1280 }, { 1344, () => Properties.Resources._1344 },
        };

        // Each resource read deserializes a new Icon, and neither the tray nor the form disposes the one it replaces;
        // loading each icon once keeps a long session from leaking a GDI handle on every status change.
        private readonly Dictionary<int, System.Drawing.Icon> iconCache = new Dictionary<int, System.Drawing.Icon>();

        /// <summary>
        /// Returns the status icon for an AppIconIdx combination. Never null: a combination with no icon of its own is
        /// first reduced to the nearest one that has.
        /// </summary>
        private System.Drawing.Icon GetIcon(int IconIdx)
        {
            int key = NormalizeIconIdx(IconIdx);
            if (!iconCache.TryGetValue(key, out System.Drawing.Icon icon))
            {
                icon = StatusIcons[key]();
                iconCache[key] = icon;
            }
            return icon;
        }

        /// <summary>
        /// The same icon cut to one frame size. The notification area shows whatever frame the handle carries, and an
        /// Icon read from resources carries the 32 px one, which the shell then shrinks to a blurred 16 px; asking for
        /// SystemInformation.SmallIconSize hands it the frame drawn for that size.
        /// </summary>
        private System.Drawing.Icon GetIcon(int IconIdx, Size size)
        {
            int key = NormalizeIconIdx(IconIdx) | (size.Width << 16);
            if (!iconCache.TryGetValue(key, out System.Drawing.Icon icon))
            {
                icon = new System.Drawing.Icon(GetIcon(IconIdx), size);
                iconCache[key] = icon;
            }
            return icon;
        }

        /// <summary>
        /// Maps any combination of AppIconIdx flags onto one of the 24 status icons, so an unforeseen combination can
        /// never leave the tray without an icon.
        /// </summary>
        private static int NormalizeIconIdx(int IconIdx)
        {
            const int batteryBits = (int)(AppIconIdx.IDX_BATT_0 | AppIconIdx.IDX_BATT_25 | AppIconIdx.IDX_BATT_50 |
                                          AppIconIdx.IDX_BATT_75 | AppIconIdx.IDX_BATT_100);
            int offset = (int)AppIconIdx.IDX_OFFSET;
            int dark = IconIdx & (int)AppIconIdx.WIN_DARK;

            // Retry and offline describe the connection, which outranks any battery reading still in the flags.
            if ((IconIdx & (int)AppIconIdx.IDX_ICO_RETRY) != 0)
            {
                return offset | (int)AppIconIdx.IDX_ICO_RETRY | dark;
            }
            if ((IconIdx & (int)AppIconIdx.IDX_ICO_OFFLINE) != 0)
            {
                return offset | (int)AppIconIdx.IDX_ICO_OFFLINE | dark;
            }

            int battery = IconIdx & batteryBits;
            if (battery == 0)
            {
                // No charge level to draw. The connection is fine - offline and retry were handled above - so the
                // offline icon would be a lie; show a level that makes no claim of danger instead.
                battery = (IconIdx & (int)AppIconIdx.IDX_OL) != 0
                    ? (int)AppIconIdx.IDX_BATT_100
                    : (int)AppIconIdx.IDX_BATT_50;
            }

            // A status carrying neither OL nor OB keeps the previous flags, so two levels can end up set together;
            // the lower one is the cautious reading.
            battery &= -battery;
            return offset | battery | (IconIdx & (int)AppIconIdx.IDX_OL) | dark;
        }

        /// <summary>
        /// Reverses a string, replacing the VB runtime's StrReverse so this port carries no Microsoft.VisualBasic
        /// dependency.
        /// </summary>
        private static string StrReverse(string value)
        {
            if (value == null)
            {
                return null;
            }

            var chars = value.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        private void Menu_UPS_Var_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Open List Var Gui", LogLvl.LOG_DEBUG, this);
            var lvgForm = new List_Var_Gui(UPS_Device);
            lvgForm.Show();
        }

        private void AddLogLine(string logLine)
        {
            // The logger is also written from the connection worker thread, so this can arrive off the UI thread.
            if (!MarshalToUi(new Action<string>(AddLogLine), logLine))
            {
                return;
            }

            if (!CB_CurrentLog.Items.Contains(logLine))
            {
                // Invert insertions so latest items appear at the top.
                CB_CurrentLog.Items.Insert(0, logLine);
                CB_CurrentLog.SelectedIndex = 0;
            }

            // Outside the guard above, and guarded on its own contents instead. A line logged during Load
            // reaches the combo box before the dashboard exists, so by the time the backlog is replayed the
            // combo box already holds it - and the model, which is what the user actually reads, would never
            // be given it at all.
            if (DashboardModel != null && !DashboardModel.LogHistory.Contains(logLine))
            {
                DashboardModel.LatestLogLine = logLine;
                DashboardModel.LogHistory.Insert(0, logLine);
            }
        }

        private void TrimLogLine(string removedLine)
        {
            if (!MarshalToUi(new Action<string>(TrimLogLine), removedLine))
            {
                return;
            }

            LogFile.LogTracing("Receiving event to trim end of displayed logs list.", LogLvl.LOG_DEBUG, this);
            CB_CurrentLog.Items.Remove(removedLine);
            DashboardModel?.LogHistory.Remove(removedLine);
        }

        /// <summary>
        /// Returns True when the caller is already on the UI thread and may continue. Otherwise re-posts the call
        /// and returns False.
        /// </summary>
        private bool MarshalToUi(Delegate handler, object arg)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return false;
            }

            if (!InvokeRequired)
            {
                return true;
            }

            try
            {
                BeginInvoke(handler, arg);
            }
            catch
            {
                // The window is going away; the message is not worth chasing.
            }

            return false;
        }

        private void HandleUPSStatusChange(UPS_Device sender, UPS_States newStatuses)
        {
            LogFile.LogTracing("Handling new UPS status(es)...", LogLvl.LOG_DEBUG, this);

            if (newStatuses.Equals(UPS_States.None))
            {
                LogFile.LogTracing("Received unexpected None status from UPS.", LogLvl.LOG_WARNING, this);
                // Determine if we need to initiate the stop procedure.

            }
            else if (Properties.Settings.Default.PW_RespectFSD && newStatuses.HasFlag(UPS_States.FSD))
            {
                LogFile.LogTracing("Full Shut Down imposed by the NUT server.", LogLvl.LOG_NOTICE, this, StrLog[(int)AppResxStr.STR_LOG_NUT_FSD]);
                Shutdown_Event(true);

            }
            else if (newStatuses.HasFlag(UPS_States.OB))
            {
                LogFile.LogTracing(sender.Name + " has switched to battery power.", LogLvl.LOG_NOTICE, this);

            }
            else if (newStatuses.HasFlag(UPS_States.OL) && ShutdownStatus)
            {
                LogFile.LogTracing("UPS returned online during a pre-shutdown event.", LogLvl.LOG_NOTICE, this);
                Stop_Shutdown_Event();
            }
        }

        /// <summary>
        /// Track if the Shutdown_Gui is counting down to activate the Shutdown_Action.
        /// </summary>
        private bool ShutdownStatus = false;

        /// <param name="forcedByServer">True for an FSD from the NUT server, false for the battery limits.</param>
        private void Shutdown_Event(bool forcedByServer)
        {
            var alreadyRunning = ShutdownStatus;
            ShutdownStatus = true;

            // The FSD branch does not check ShutdownStatus before coming here, so an FSD raised again while a
            // countdown is under way re-enters with the same shutdown; the user is told about it only the first time. Told before
            // anything below, because the immediate path ends in Shutdown_Action, and the email has to be queued
            // by then for the flush there to send it.
            if (!alreadyRunning)
            {
                Notify(center => center.NotifyShutdownStarted(forcedByServer), "shutdown started");
            }

            if (Properties.Settings.Default.PW_Immediate)
            {
                LogFile.LogTracing("Immediately stopping due to shutdown event.", LogLvl.LOG_NOTICE, this);
                UPSDisconnect();
                Shutdown_Action();
            }
            else
            {
                LogFile.LogTracing("Open Shutdown Gui", LogLvl.LOG_DEBUG, this);
                Shutdown_Gui.Instance.Activate();
                Shutdown_Gui.Instance.Visible = true;
                HasFocus = false;
            }
        }

        private void Stop_Shutdown_Event()
        {
            var wasRunning = ShutdownStatus;
            ShutdownStatus = false;

            // A "cancelled" message for a shutdown the user was never told about would only alarm them.
            if (wasRunning)
            {
                Notify(center => center.NotifyShutdownCancelled(), "shutdown cancelled");
            }

            Shutdown_Gui.Instance.Shutdown_Timer.Stop();
            Shutdown_Gui.Instance.Shutdown_Timer.Enabled = false;
            Shutdown_Gui.Instance.Grace_Timer.Stop();
            Shutdown_Gui.Instance.Grace_Timer.Enabled = false;
            Shutdown_Gui.Instance.Hide();
            Shutdown_Gui.Instance.Close();
            LogFile.LogTracing("Stop condition cancelled.", LogLvl.LOG_NOTICE, this, AppGlobals.StrLog[(int)AppResxStr.STR_LOG_SHUT_STOP]);
        }

        public void Shutdown_Action()
        {
            var stopAction = Properties.Settings.Default.PW_StopType;
            LogFile.LogTracing("Windows going down, NutDesk will disconnect.", LogLvl.LOG_NOTICE, this, StrLog[(int)AppResxStr.STR_MAIN_GOTOSLEEP]);
            UPSDisconnect();

            FlushNotificationEmail();

            // Stub out stop action when debugging.
#if DEBUG
            if (Debugger.IsAttached)
            {
                LogFile.LogTracing("Aborting stopAction " + stopAction + " due to attached debugger.", LogLvl.LOG_NOTICE, this);
                return;
            }
#else
            switch (stopAction)
            {
                case 0:
                    Process.Start(@"C:\WINDOWS\system32\Shutdown.exe", "-f -s -t 0");
                    break;
                case 1:
                    Application.SetSuspendState(PowerState.Suspend, false, true);
                    break;
                case 2:
                    Application.SetSuspendState(PowerState.Hibernate, false, true);
                    break;
            }
#endif
        }

        /// <summary>
        /// Gives the emails still in the outbox - the "shutting down" one above all - a last chance to leave.
        /// </summary>
        /// <remarks>
        /// This is the last moment they can: once the power action runs, the network and this process go with it,
        /// and a mail left queued would only be sent when the computer is back, long after it mattered. It blocks
        /// the UI thread, but for a bounded ten seconds, and the countdown before this point has already given the
        /// user their warning. Run before the debugger check so the path can be exercised without losing the machine.
        /// </remarks>
        private void FlushNotificationEmail()
        {
            var center = notifications;
            if (center == null)
            {
                return;
            }

            try
            {
                if (center.FlushEmail(TimeSpan.FromSeconds(10)))
                {
                    LogFile.LogTracing("Notification outbox emptied before the stop action.", LogLvl.LOG_NOTICE, this);
                }
                else
                {
                    LogFile.LogTracing("Notification emails still pending at the stop action; they will be retried at the next start.",
                                       LogLvl.LOG_WARNING, this);
                }
            }
            catch (Exception ex)
            {
                // The computer has to go down regardless of whether the email could leave.
                LogFile.LogTracing("Unable to flush the notification outbox before the stop action.", LogLvl.LOG_WARNING, this);
                LogFile.LogException(ex, this);
            }
        }

        private void ManageOldPrefsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Launching UpgradePrefsDialog from ToolStripMenu.", LogLvl.LOG_NOTICE, this);
            UPSDisconnect();
            NutDesk.Forms.UpgradePrefsDialog.Instance.ShowDialog();
        }

        #region "Updates"

        // The first automatic check waits this long after startup: NutDesk usually starts at logon, often before the
        // network is up, and a check that failed then would only be tried again an hour later.
        private static readonly TimeSpan UpdateCheckStartupDelay = TimeSpan.FromMinutes(1);

        // After that, how often the timer looks whether a check is due. The interval chosen in the preferences - a day,
        // a week, a month - decides whether one runs; this only bounds how late it can be, and how soon a check that
        // failed is tried again.
        private static readonly TimeSpan UpdateCheckTimerInterval = TimeSpan.FromHours(1);

        private System.Windows.Forms.Timer UpdateCheckTimer;

        /// <summary>The open update window, or null. There is never more than one.</summary>
        private Wpf.UpdateWindow UpdateView;

        // True while a check started from here waits for GitHub: another request joins it instead of sending a second.
        private bool UpdateCheckPending;

        // Whether the pending check answers the user: true as soon as one of the requests it stands for was manual.
        private bool UpdateCheckIsManual;

        private void StartUpdateCheckTimer()
        {
            if (UpdateCheckTimer != null)
            {
                return;
            }

            UpdateCheckTimer = new System.Windows.Forms.Timer { Interval = (int)UpdateCheckStartupDelay.TotalMilliseconds };
            UpdateCheckTimer.Tick += UpdateCheckTimer_Tick;
            UpdateCheckTimer.Start();
        }

        private void StopUpdateCheckTimer()
        {
            if (UpdateCheckTimer == null)
            {
                return;
            }

            UpdateCheckTimer.Stop();
            UpdateCheckTimer.Tick -= UpdateCheckTimer_Tick;
            UpdateCheckTimer.Dispose();
            UpdateCheckTimer = null;
        }

        private void UpdateCheckTimer_Tick(object sender, EventArgs e)
        {
            // The first tick ends the startup delay; every later one comes an hour after the one before.
            UpdateCheckTimer.Interval = (int)UpdateCheckTimerInterval.TotalMilliseconds;
            RunAutomaticUpdateCheck(false);
        }

        /// <summary>
        /// Runs an automatic check if one is due: checking is on, the interval since the last check that reached
        /// GitHub has passed, and nothing about updates is under way - no check, download or installation, and no
        /// update window open.
        /// </summary>
        /// <param name="evenIfNotDue">The debug menu's shortcut: leaves out the setting and the interval.</param>
        private void RunAutomaticUpdateCheck(bool evenIfNotDue)
        {
            if (!evenIfNotDue &&
                (!Properties.Settings.Default.UP_CheckAtStart ||
                 !Updater.UpdateUtil.UpdateCheckDelayPassed(Properties.Settings.Default.UP_AutoChkDelay, Properties.Settings.Default.UP_LastCheck)))
            {
                return;
            }

            if (UpdateController.IsBusy || UpdateCheckPending || UpdateView != null)
            {
                LogFile.LogTracing("Automatic update check due, but an update is already being handled.", LogLvl.LOG_DEBUG, this);
                return;
            }

            LogFile.LogTracing("Automatic update check due.", LogLvl.LOG_DEBUG, this);
            StartUpdateCheck(false);
        }

        /// <summary>Help > Verify Update, and the same item in the tray menu.</summary>
        private void Menu_Update_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Check for update menu item clicked.", LogLvl.LOG_DEBUG, this);
            StartUpdateCheck(true);
        }

        /// <summary>
        /// Checks for an update and handles the answer. Never throws: CheckForUpdateAsync reports a failure in its
        /// result, and completes back on the UI thread.
        /// </summary>
        /// <param name="manual">True when the user asked for the check: the answer is then always shown.</param>
        private async void StartUpdateCheck(bool manual)
        {
            // One update window at a time. While one is open it is the answer, and it may be downloading.
            if (UpdateView != null)
            {
                if (manual)
                {
                    BringUpdateWindowForward();
                }

                return;
            }

            // The new version is already installing itself, and this one is on its way out.
            if (UpdateController.IsInstallStarted)
            {
                return;
            }

            // A check is already on its way - an automatic one, or the first click of two: it answers this request too.
            if (UpdateCheckPending)
            {
                UpdateCheckIsManual |= manual;
                return;
            }

            UpdateCheckPending = true;
            UpdateCheckIsManual = manual;

            Updater.UpdateCheckResult result;
            try
            {
                LogFile.LogTracing("Beginning update check in background...", LogLvl.LOG_NOTICE, this, Properties.Resources.LogCheckingForUpdate);
                result = await UpdateController.CheckForUpdateAsync(Properties.Settings.Default.UP_Branch == 1, manual);
            }
            finally
            {
                UpdateCheckPending = false;
            }

            // Whether to answer the user is decided here, not by result.IsManual: a manual request that joined an
            // automatic check is not in the result.
            try
            {
                OnCheckForUpdateCompleted(result, UpdateCheckIsManual);
            }
            catch (Exception ex)
            {
                // An async void method has nobody to throw to but the crash dialog.
                LogFile.LogTracing("Unable to handle the result of the update check.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);
            }
        }

        private void OnCheckForUpdateCompleted(Updater.UpdateCheckResult result, bool manual)
        {
            LogFile.LogTracing("Update check completed" + (manual ? " (manual): " : ": ") + result, LogLvl.LOG_DEBUG, this);
            if (IsDisposed || UpdateController.IsInstallStarted)
            {
                return;
            }

            // Only a check that reached GitHub counts. A failed one - no network yet at logon, a proxy, a rate
            // limit - must be tried again, not put off for a whole interval.
            if (result.ReachedServer)
            {
                Properties.Settings.Default.UP_LastCheck = DateTime.Now;
                Properties.Settings.Default.Save();
            }

            if (result.Outcome == Updater.UpdateCheckOutcome.Failed)
            {
                // A warning with its reason in the user's language, so it shows in the event log; UpdateUtil has logged
                // the exception. Only a user who asked is interrupted with it.
                var failure = Updating.UpdateText.CheckFailedMessage(result);
                LogFile.LogTracing($"Update check failed ({result.Failure}): {result.ErrorDetail}", LogLvl.LOG_WARNING, this, failure);
                if (manual)
                {
                    ShowUpdateMessage(failure, MessageBoxIcon.Warning);
                }

                return;
            }

            if (result.Outcome == Updater.UpdateCheckOutcome.UpToDate)
            {
                LogFile.LogTracing("No newer version available.", LogLvl.LOG_NOTICE, this, StrLog[(int)AppResxStr.STR_LOG_NO_UPDATE]);
                if (manual)
                {
                    ShowUpdateMessage(Updating.UpdateText.UpToDateMessage(), MessageBoxIcon.Information);
                }

                return;
            }

            // The user said "skip this version": automatic checks keep quiet about it, a manual one still shows it.
            if (!manual && result.IsSameRelease(Properties.Settings.Default.UP_SkippedVersion))
            {
                LogFile.LogTracing($"Update {result.VersionKey} available, but skipped by the user.", LogLvl.LOG_NOTICE, this);
                return;
            }

            LogFile.LogTracing($"New update found: {result.VersionKey}", LogLvl.LOG_NOTICE, this,
                               string.Format(StrLog[(int)AppResxStr.STR_LOG_UPDATE], result.VersionText));

            // The toast and the email go out once per version, however many checks find it again.
            if (!result.IsSameRelease(Properties.Settings.Default.UP_NotifiedVersion))
            {
                Notify(center => center.NotifyUpdateAvailable(result.VersionText), "an available update");
                Properties.Settings.Default.UP_NotifiedVersion = result.VersionKey;
                Properties.Settings.Default.Save();
            }

            // A manual check brings the window up in front. An automatic one does not take the keyboard away from
            // whatever the user is typing into.
            ShowUpdateWindow(result, manual);
        }

        /// <summary>
        /// Opens the update window for <paramref name="result"/>, or brings forward the one already open.
        /// </summary>
        /// <param name="activate">False to show the window without giving it the focus.</param>
        private void ShowUpdateWindow(Updater.UpdateCheckResult result, bool activate)
        {
            if (UpdateView != null)
            {
                BringUpdateWindowForward();
                return;
            }

            LogFile.LogTracing("Opening the update window for " + result.VersionKey + ".", LogLvl.LOG_DEBUG, this);

            try
            {
                var window = new Wpf.UpdateWindow(result, Theme.IsDark, GetUpdateInstallBlocker, ExitForUpdate);
                window.Closed += (s, e) =>
                {
                    if (ReferenceEquals(UpdateView, window))
                    {
                        UpdateView = null;
                    }
                };

                // Owned by the main window only while that is on screen. An owned window disappears whenever its owner
                // is minimised, and an unattended NutDesk spends its time in the tray: a window owned by it then would
                // open where nobody can see it. Unowned, it has a taskbar button of its own.
                if (Visible && WindowState != FormWindowState.Minimized)
                {
                    new System.Windows.Interop.WindowInteropHelper(window).Owner = Handle;
                    window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                }
                else
                {
                    window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                }

                window.ShowActivated = activate;
                UpdateView = window;
                window.Show();

                if (activate)
                {
                    HasFocus = false;
                }
            }
            catch (Exception ex)
            {
                // A menu action or a timer must not be able to take the application down.
                LogFile.LogTracing("Unable to open the update window.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(ex, this);
                UpdateView = null;
            }
        }

        private void BringUpdateWindowForward()
        {
            var window = UpdateView;
            if (window == null)
            {
                return;
            }

            // Owned by a main window that has gone to the tray since, it went with it and comes back with it.
            if (!window.IsVisible)
            {
                RestoreFromTray();
            }

            if (window.WindowState == System.Windows.WindowState.Minimized)
            {
                window.WindowState = System.Windows.WindowState.Normal;
            }

            window.Activate();
        }

        /// <summary>
        /// Asked by the update window right before it installs: why the installation has to wait, or null when it may
        /// go ahead. NutDesk is not watching the UPS for the few seconds it takes to restart, and must not be away then.
        /// </summary>
        private string GetUpdateInstallBlocker()
        {
            if (ShutdownStatus)
            {
                return Properties.Resources.Update_BlockedByShutdown;
            }

            // The reading the dashboard shows as "on battery".
            var connected = UPS_Device?.IsConnected ?? false;
            var status = UPS_Device?.UPS_Datas?.UPS_Value?.UPS_Status;
            if (connected && status.HasValue && status.Value.HasFlag(UPS_States.OB))
            {
                return Properties.Resources.Update_BlockedByBattery;
            }

            return null;
        }

        /// <summary>
        /// The update window has started the new version, which waits for this process to end before it copies itself
        /// over this folder. So exit for real - not to the tray - the way Exit in the menu does: the settings are saved
        /// and the notification outbox is written to disk on the way out, for the new version to send.
        /// </summary>
        private void ExitForUpdate()
        {
            LogFile.LogTracing("Exiting so that the new version can install itself.", LogLvl.LOG_NOTICE, this);

            // Posted: this is called from the update window's button, which has to return before its window goes.
            BeginInvoke(new Action(() =>
            {
                StopUpdateCheckTimer();
                UpdateView?.Close();
                Application.Exit();
            }));
        }

        /// <summary>
        /// A message about updates: over the main window when that is on screen, on its own when NutDesk is in the tray.
        /// </summary>
        private void ShowUpdateMessage(string message, MessageBoxIcon icon)
        {
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                MessageBox.Show(this, message, ProgramName, MessageBoxButtons.OK, icon);
            }
            else
            {
                MessageBox.Show(message, ProgramName, MessageBoxButtons.OK, icon);
            }
        }

        #endregion

        private void Menu_Persist_CheckedChanged(object sender, EventArgs e)
        {
            LogFile.LogTracing("Menu_Persist checked state changing to " + Menu_Persist.Checked, LogLvl.LOG_DEBUG, this);
            Properties.Settings.Default.NUT_AutoReconnect = Menu_Persist.Checked;
        }
    }
}
