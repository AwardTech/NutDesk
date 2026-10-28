using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Threading;
using NutDesk.Common;

namespace NutDesk.Wpf
{
    /// <summary>Displays commands advertised by the active NUT connection.</summary>
    public partial class UPSCommandsWindow : Window
    {
        private readonly Func<UPS_Device> getDevice;
        private readonly HashSet<string> advertisedCommands = new HashSet<string>(StringComparer.Ordinal);
        private readonly DispatcherTimer connectionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        private readonly DispatcherTimer batteryTestStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        private readonly DispatcherTimer beeperStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        private UPS_Device observedDevice;
        private UPS_Device.ConnectedEventHandler observedConnectedHandler;
        private UPS_Device.Lost_ConnectEventHandler observedLostConnectHandler;
        private UPS_Device.DisconnectedEventHandler observedDisconnectedHandler;
        private bool paletteIsDark;
        private bool isRefreshing;
        private bool commandInProgress;
        private bool refreshPending;
        private bool batteryTestStatusPollInProgress;
        private bool batteryTestStatusUnsupported;
        private bool beeperStatusPollInProgress;
        private bool isClosed;
        private string lastBatteryTestResult;
        private string commandOutcomeNotice;
        private string commandInProgressName;
        private int connectionGeneration;

        public UPSCommandsWindow(Func<UPS_Device> getDevice, bool isDark)
        {
            this.getDevice = getDevice;
            InitializeComponent();
            paletteIsDark = isDark;
            Resources.MergedDictionaries.Insert(0, Palette.Build(isDark));
            ElementHost.EnableModelessKeyboardInterop(this);
            connectionTimer.Tick += CheckActiveDevice;
            batteryTestStatusTimer.Tick += BatteryTestStatusTimer_Tick;
            beeperStatusTimer.Tick += BeeperStatusTimer_Tick;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        internal void ApplyPalette(bool isDark)
        {
            if (isDark == paletteIsDark && Resources.MergedDictionaries.Count > 0)
            {
                return;
            }

            paletteIsDark = isDark;
            Resources.MergedDictionaries[0] = Palette.Build(isDark);
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadCommandsAsync();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            connectionTimer.Start();
            batteryTestStatusTimer.Start();
            beeperStatusTimer.Start();
            BindToActiveDevice();
            RefreshUnsupportedVariablesList();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            isClosed = true;
            refreshPending = false;
            connectionGeneration++;
            connectionTimer.Stop();
            batteryTestStatusTimer.Stop();
            beeperStatusTimer.Stop();
            ObserveDevice(null);
        }

        private void CheckActiveDevice(object sender, EventArgs e)
        {
            UPS_Device activeDevice = getDevice == null ? null : getDevice();
            UpdateCommandListHeading(activeDevice);

            if (!ReferenceEquals(observedDevice, activeDevice))
            {
                BindToActiveDevice();
            }
            else
            {
                UpdateCommandButtons();
            }
        }

        private void BindToActiveDevice()
        {
            UPS_Device device = getDevice == null ? null : getDevice();
            UpdateCommandListHeading(device);
            if (ReferenceEquals(observedDevice, device))
            {
                UpdateCommandButtons();
                return;
            }

            connectionGeneration++;
            refreshPending = false;
            commandOutcomeNotice = null;
            batteryTestStatusUnsupported = false;
            lastBatteryTestResult = null;
            SetBatteryTestStatus(device == null || !device.IsConnected
                ? "Connect to a UPS to read its battery-test status."
                : "Checking UPS test status...", null);
            ObserveDevice(device);
            advertisedCommands.Clear();
            CommandList.Items.Clear();
            StatusText.Text = device == null || !device.IsConnected
                ? "Connect to a UPS to load its command list."
                : "Connected. Retrieving command list...";
            UpdateCommandButtons();
            RefreshUnsupportedVariablesList();

            if (device != null && device.IsConnected && !device.IsConnecting)
            {
                _ = LoadCommandsAsync();
            }
        }

        private void ObserveDevice(UPS_Device device)
        {
            if (observedDevice != null)
            {
                observedDevice.Connected -= observedConnectedHandler;
                observedDevice.Lost_Connect -= observedLostConnectHandler;
                observedDevice.Disconnected -= observedDisconnectedHandler;
            }

            observedDevice = device;

            if (observedDevice != null)
            {
                var source = observedDevice;
                observedConnectedHandler = sender => OnDeviceConnected(source, sender);
                observedLostConnectHandler = () => OnDeviceDisconnected(source);
                observedDisconnectedHandler = () => OnDeviceDisconnected(source);
                observedDevice.Connected += observedConnectedHandler;
                observedDevice.Lost_Connect += observedLostConnectHandler;
                observedDevice.Disconnected += observedDisconnectedHandler;
            }
            else
            {
                observedConnectedHandler = null;
                observedLostConnectHandler = null;
                observedDisconnectedHandler = null;
            }
        }

        private void OnDeviceConnected(UPS_Device source, UPS_Device sender)
        {
            RunOnUiThread(() =>
            {
                if (isClosed || !ReferenceEquals(source, observedDevice) || !ReferenceEquals(sender, source) ||
                    !ReferenceEquals(source, getDevice == null ? null : getDevice()))
                {
                    return;
                }

                connectionGeneration++;
                advertisedCommands.Clear();
                UpdateCommandListHeading(source);
                UpdateCommandButtons();
                RefreshUnsupportedVariablesList();
                _ = LoadCommandsAsync();
            });
        }

        private void OnDeviceDisconnected(UPS_Device source)
        {
            RunOnUiThread(() =>
            {
                if (isClosed || !ReferenceEquals(source, observedDevice) ||
                    !ReferenceEquals(source, getDevice == null ? null : getDevice()))
                {
                    return;
                }

                connectionGeneration++;
                refreshPending = false;
                batteryTestStatusUnsupported = false;
                lastBatteryTestResult = null;
                SetBatteryTestStatus("UPS disconnected. Battery-test status is unavailable.", null);
                advertisedCommands.Clear();
                CommandList.Items.Clear();
                if (commandInProgress)
                {
                    commandOutcomeNotice = "The command outcome is unknown because the UPS connection was lost.";
                    GetCommandFeedbackText(commandInProgressName).Text = commandOutcomeNotice;
                }
                else
                {
                    StatusText.Text = "UPS disconnected. Command buttons are disabled.";
                }
                UpdateCommandButtons();
                RefreshUnsupportedVariablesList();
            });
        }

        private void RunOnUiThread(Action action)
        {
            if (isClosed)
            {
                return;
            }

            if (Dispatcher.CheckAccess())
            {
                action();
            }
            else if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                Dispatcher.BeginInvoke(action);
            }
        }

        private async Task LoadCommandsAsync()
        {
            if (isClosed)
            {
                return;
            }

            refreshPending = true;
            if (isRefreshing || commandInProgress)
            {
                return;
            }

            isRefreshing = true;
            refreshPending = false;
            UPS_Device device = observedDevice;
            int requestGeneration = connectionGeneration;
            RefreshButton.IsEnabled = false;
            StatusText.Text = "Retrieving command list...";
            advertisedCommands.Clear();
            CommandList.Items.Clear();
            UpdateCommandButtons();

            try
            {
                if (device == null || !device.IsConnected || device.IsConnecting)
                {
                    throw new InvalidOperationException("Wait for the UPS connection to finish before retrieving its command list.");
                }

                List<string> commands = await Task.Run(() => device.GetUPSCommands());
                if (!IsCurrentRequest(device, requestGeneration))
                {
                    return;
                }

                if (!device.IsConnected)
                {
                    throw new InvalidOperationException("The UPS disconnected while retrieving the command list.");
                }

                foreach (string command in commands)
                {
                    CommandList.Items.Add(command);
                    advertisedCommands.Add(command);
                }

                StatusText.Text = AddCommandOutcomeNotice(commands.Count == 0
                    ? "The server did not advertise any instant commands."
                    : string.Format("{0} command(s) advertised.", commands.Count));
            }
            catch (Exception ex)
            {
                bool isCurrentNoDeviceRequest = device == null && requestGeneration == connectionGeneration &&
                                                !isClosed && observedDevice == null &&
                                                (getDevice == null || getDevice() == null);
                if (isCurrentNoDeviceRequest)
                {
                    StatusText.Text = AddCommandOutcomeNotice(
                        "No UPS device is currently connected. Check your connection settings and connect to a UPS before refreshing the command list.");
                }
                else if (IsCurrentRequest(device, requestGeneration))
                {
                    advertisedCommands.Clear();
                    CommandList.Items.Clear();
                    StatusText.Text = AddCommandOutcomeNotice("Unable to retrieve the command list: " + ex.Message);
                }
            }
            finally
            {
                isRefreshing = false;
                UpdateCommandButtons();
                StartPendingRefreshIfNeeded();
            }
        }

        private bool IsCurrentRequest(UPS_Device device, int requestGeneration)
        {
            return requestGeneration == connectionGeneration && IsActiveDevice(device);
        }

        private bool IsActiveDevice(UPS_Device device)
        {
            return !isClosed && device != null && ReferenceEquals(device, observedDevice) &&
                   ReferenceEquals(device, getDevice == null ? null : getDevice());
        }

        private string lastConfirmedBeeperStatus;

        private async void BeeperStatusTimer_Tick(object sender, EventArgs e)
        {
            if (isClosed || beeperStatusPollInProgress || isRefreshing || commandInProgress) return;
            UPS_Device device = observedDevice;
            if (device == null || !device.IsConnected || device.IsConnecting) return;

            beeperStatusPollInProgress = true;
            int requestGeneration = connectionGeneration;
            try
            {
                string result = await Task.Run(() => device.GetUPSVar("ups.beeper.status"));
                if (!IsCurrentRequest(device, requestGeneration) || string.IsNullOrWhiteSpace(result)) return;
                string status = result.Trim();
                if (string.Equals(status, "enabled", StringComparison.OrdinalIgnoreCase))
                {
                    lastConfirmedBeeperStatus = "Enabled";
                    BeeperStatusText.Text = lastConfirmedBeeperStatus;
                    BeeperStatusText.Foreground = Brushes.White;
                    BeeperStatusBadge.Background = new SolidColorBrush(Color.FromRgb(25, 135, 84));
                }
                else if (string.Equals(status, "disabled", StringComparison.OrdinalIgnoreCase))
                {
                    lastConfirmedBeeperStatus = "Disabled";
                    BeeperStatusText.Text = lastConfirmedBeeperStatus;
                    BeeperStatusBadge.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                    BeeperStatusText.Foreground = new SolidColorBrush(Color.FromRgb(33, 37, 41));
                }
            }
            catch
            {
                // Keep the last server-confirmed status and colour. Never infer status from a command click.
            }
            finally
            {
                beeperStatusPollInProgress = false;
                RefreshUnsupportedVariablesList();
            }
        }

        private void RefreshUnsupportedVariablesList()
        {
            UnsupportedVariablesList.Items.Clear();
            UPS_Device device = observedDevice;
            if (device == null)
            {
                UnsupportedVariablesList.Items.Add("Connect to a UPS to view unsupported variables.");
                ClearUnsupportedVariablesButton.IsEnabled = false;
                return;
            }

            List<string> variables = device.GetUnsupportedVariablesForCurrentUPS();
            foreach (string variable in variables) UnsupportedVariablesList.Items.Add(variable);
            if (variables.Count == 0) UnsupportedVariablesList.Items.Add("None identified.");
            ClearUnsupportedVariablesButton.IsEnabled = variables.Count > 0;
        }

        private void ClearUnsupportedVariablesButton_Click(object sender, RoutedEventArgs e)
        {
            UPS_Device device = observedDevice;
            if (device == null) return;
            device.ClearUnsupportedVariablesForCurrentUPS();
            RefreshUnsupportedVariablesList();
        }

        private async void BatteryTestStatusTimer_Tick(object sender, EventArgs e)
        {
            if (isClosed || batteryTestStatusPollInProgress || batteryTestStatusUnsupported ||
                isRefreshing || commandInProgress)
            {
                return;
            }

            UPS_Device device = observedDevice;
            if (device == null || !device.IsConnected || device.IsConnecting)
            {
                SetBatteryTestStatus("Connect to a UPS to read its battery-test status.", null);
                return;
            }

            batteryTestStatusPollInProgress = true;
            int requestGeneration = connectionGeneration;

            try
            {
                string result = await Task.Run(() => device.GetUPSVar(new[] { "ups.test.result" }));
                if (!IsCurrentRequest(device, requestGeneration))
                {
                    return;
                }

                lastBatteryTestResult = result;
                SetBatteryTestStatus(DescribeBatteryTestResult(result), result);
            }
            catch (Exception ex)
            {
                if (!IsCurrentRequest(device, requestGeneration))
                {
                    return;
                }

                NutException nutException = ex as NutException;
                if (nutException != null && nutException.LastTransaction != null &&
                    nutException.LastTransaction.ResponseType == NUTResponse.VARNOTSUPPORTED)
                {
                    batteryTestStatusUnsupported = true;
                    SetBatteryTestStatus("This UPS does not provide battery-test status.", "Unsupported");
                }
                else if (!device.IsConnected)
                {
                    SetBatteryTestStatus("UPS connection lost. Battery-test status is unavailable.", null);
                }
                else
                {
                    // Keep the last good result visible if a single telemetry read fails.
                    SetBatteryTestStatus(lastBatteryTestResult == null
                        ? "Unable to read battery-test status."
                        : DescribeBatteryTestResult(lastBatteryTestResult), lastBatteryTestResult);
                }
            }
            finally
            {
                batteryTestStatusPollInProgress = false;
            }
        }

        private void SetBatteryTestStatus(string message, string rawResult)
        {
            BatteryTestStatusText.Text = message;
            BatteryTestRawResultText.Text = string.IsNullOrWhiteSpace(rawResult)
                ? "NUT result: —"
                : "NUT result: " + rawResult;
        }

        private static string DescribeBatteryTestResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return "The UPS returned an empty battery-test result.";
            }

            switch (result.Trim())
            {
                case "TestInProgress":
                    return "Battery self-test in progress.";
                case "Done and passed":
                    return "Battery self-test completed successfully.";
                case "Aborted":
                    return "Battery self-test aborted.";
                case "Done and failed":
                    return "Battery self-test failed.";
                case "No battery":
                    return "Battery self-test unavailable: no battery detected.";
                case "Invalid test":
                    return "The UPS reported an invalid battery-test request.";
                default:
                    return "Battery-test result: " + result;
            }
        }

        private void UpdateCommandListHeading(UPS_Device device)
        {
            // Keep the compact two-column panel heading independent of the active UPS name.
            CommandListHeading.Text = "Supported Instant Commands";
        }

        private string AddCommandOutcomeNotice(string status)
        {
            if (string.IsNullOrEmpty(commandOutcomeNotice))
            {
                return status;
            }

            string result = status + " " + commandOutcomeNotice;
            commandOutcomeNotice = null;
            return result;
        }

        private void StartPendingRefreshIfNeeded()
        {
            if (!isClosed && refreshPending && !isRefreshing && !commandInProgress &&
                observedDevice != null && observedDevice.IsConnected && !observedDevice.IsConnecting)
            {
                _ = LoadCommandsAsync();
            }
        }

        private async void InstantCommand_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            string command = button == null ? null : button.Tag as string;
            if (button == null || !button.IsEnabled || string.IsNullOrEmpty(command) ||
                !advertisedCommands.Contains(command) || isRefreshing || commandInProgress)
            {
                return;
            }

            UPS_Device device = observedDevice;
            if (device == null || !IsCurrentRequest(device, connectionGeneration) || !device.IsConnected)
            {
                UpdateCommandButtons();
                return;
            }

            string action = Convert.ToString(button.Content);
            string upsName = device.Name;
            int commandGeneration = connectionGeneration;
            bool isPowerCommand = IsPowerCommand(command);
            string confirmationMessage = isPowerCommand
                ? string.Format(
                    "This command may interrupt power to devices and appliances connected to the UPS. As a consequence, unsaved work may be lost and data may be corrupted.\n\nCommand: {0}\nUPS: {1}\n\nAre you sure you want to continue?",
                    command, upsName)
                : string.Format("Do you want to {0} on UPS '{1}'?", action, upsName);

            MessageBoxResult answer = MessageBox.Show(this,
                confirmationMessage,
                isPowerCommand ? "Confirm UPS power command" : "Confirm UPS command",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                if (isPowerCommand)
                {
                    PowerCommandFeedbackText.Text = "Power command cancelled. No command was sent to the UPS.";
                }
                return;
            }

            // The connection and advertised capability can change while the confirmation dialog is open.
            if (commandGeneration != connectionGeneration ||
                !ReferenceEquals(device, getDevice == null ? null : getDevice()) || !device.IsConnected ||
                !advertisedCommands.Contains(command))
            {
                GetCommandFeedbackText(command).Text =
                    "The UPS connection or advertised commands changed. Refresh the command list.";
                UpdateCommandButtons();
                return;
            }

            commandInProgress = true;
            commandInProgressName = command;
            commandOutcomeNotice = null;
            UpdateCommandButtons();
            System.Windows.Controls.TextBlock feedbackText = GetCommandFeedbackText(command);
            feedbackText.Text = string.Format("Sending '{0}' to UPS [{1}]...", command, upsName);
            try
            {
                Transaction transaction = await Task.Run(() => device.ExecuteUPSCommand(command));
                if (!IsCurrentRequest(device, commandGeneration))
                {
                    return;
                }

                if (command.StartsWith("test.battery.", StringComparison.Ordinal))
                {
                    feedbackText.Text = string.Format(
                        "NUT acknowledged '{0}' for UPS [{1}].{2}\nAcknowledgement does not confirm that the battery test completed.",
                        command, upsName, TrackingAcknowledgement(transaction));
                }
                else
                {
                    feedbackText.Text = string.Format("NUT acknowledged '{0}' for UPS [{1}].{2}",
                                                    command, upsName, TrackingAcknowledgement(transaction));
                }
            }
            catch (Exception ex)
            {
                if (IsCurrentRequest(device, commandGeneration))
                {
                    // Require a fresh LIST CMD after any failed command, so no button remains enabled on stale state.
                    advertisedCommands.Clear();
                    CommandList.Items.Clear();

                    if (device.IsConnected)
                    {
                        GetCommandFeedbackText(command).Text =
                            "Command failed: " + ex.Message + " Refresh the command list before trying again.";
                    }
                    else
                    {
                        commandOutcomeNotice = "The command outcome is unknown because the UPS connection was lost.";
                        GetCommandFeedbackText(command).Text = string.Format("Command failed while sending '{0}'. {1}", command,
                                                                             commandOutcomeNotice);
                    }
                }
            }
            finally
            {
                commandInProgress = false;
                commandInProgressName = null;
                UpdateCommandButtons();
                StartPendingRefreshIfNeeded();
            }
        }

        private static bool IsPowerCommand(string command)
        {
            switch (command)
            {
                case "load.off":
                case "load.off.delay":
                case "shutdown.reboot":
                case "shutdown.stop":
                    return true;
                default:
                    return false;
            }
        }

        private System.Windows.Controls.TextBlock GetCommandFeedbackText(string command)
        {
            if (!string.IsNullOrEmpty(command) &&
                command.StartsWith("test.battery.", StringComparison.Ordinal))
            {
                return BatteryCommandFeedbackText;
            }

            if (!string.IsNullOrEmpty(command) &&
                command.StartsWith("beeper.", StringComparison.Ordinal))
            {
                return BeeperCommandFeedbackText;
            }

            return PowerCommandFeedbackText;
        }

        private static string TrackingAcknowledgement(Transaction transaction)
        {
            return transaction.SplitResponse != null && transaction.SplitResponse.Length == 3 &&
                   transaction.SplitResponse[1] == "TRACKING"
                ? " NUT tracking ID: " + transaction.SplitResponse[2] + "."
                : string.Empty;
        }

        private void UpdateCommandButtons()
        {
            bool isActiveDevice = observedDevice != null &&
                                  ReferenceEquals(observedDevice, getDevice == null ? null : getDevice());
            bool canExecute = !isRefreshing && !commandInProgress && isActiveDevice && observedDevice.IsConnected &&
                              !observedDevice.IsConnecting;
            RefreshButton.IsEnabled = !isRefreshing && !commandInProgress;
            QuickTestButton.IsEnabled = canExecute && advertisedCommands.Contains("test.battery.start.quick");
            DeepTestButton.IsEnabled = canExecute && advertisedCommands.Contains("test.battery.start.deep");
            StopTestButton.IsEnabled = canExecute && advertisedCommands.Contains("test.battery.stop");
            BeeperOnButton.IsEnabled = canExecute && advertisedCommands.Contains("beeper.on");
            BeeperOffButton.IsEnabled = canExecute && advertisedCommands.Contains("beeper.off");
            BeeperMuteButton.IsEnabled = canExecute && advertisedCommands.Contains("beeper.mute");
            BeeperEnableButton.IsEnabled = canExecute && advertisedCommands.Contains("beeper.enable");
            BeeperDisableButton.IsEnabled = canExecute && advertisedCommands.Contains("beeper.disable");
            LoadOffButton.IsEnabled = canExecute && advertisedCommands.Contains("load.off");
            LoadOffDelayButton.IsEnabled = canExecute && advertisedCommands.Contains("load.off.delay");
            ShutdownRebootButton.IsEnabled = canExecute && advertisedCommands.Contains("shutdown.reboot");
            ShutdownStopButton.IsEnabled = canExecute && advertisedCommands.Contains("shutdown.stop");
        }
    }
}
