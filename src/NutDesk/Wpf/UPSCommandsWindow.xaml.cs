using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.Integration;
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
        private UPS_Device observedDevice;
        private UPS_Device.ConnectedEventHandler observedConnectedHandler;
        private UPS_Device.Lost_ConnectEventHandler observedLostConnectHandler;
        private UPS_Device.DisconnectedEventHandler observedDisconnectedHandler;
        private bool paletteIsDark;
        private bool isRefreshing;
        private bool commandInProgress;
        private bool refreshPending;
        private bool isClosed;
        private string commandOutcomeNotice;
        private int connectionGeneration;

        public UPSCommandsWindow(Func<UPS_Device> getDevice, bool isDark)
        {
            this.getDevice = getDevice;
            InitializeComponent();
            paletteIsDark = isDark;
            Resources.MergedDictionaries.Insert(0, Palette.Build(isDark));
            ElementHost.EnableModelessKeyboardInterop(this);
            connectionTimer.Tick += CheckActiveDevice;
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
            BindToActiveDevice();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            isClosed = true;
            refreshPending = false;
            connectionGeneration++;
            connectionTimer.Stop();
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
            ObserveDevice(device);
            advertisedCommands.Clear();
            CommandList.Items.Clear();
            StatusText.Text = device == null || !device.IsConnected
                ? "Connect to a UPS to load its command list."
                : "Connected. Retrieving command list...";
            UpdateCommandButtons();

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
                advertisedCommands.Clear();
                CommandList.Items.Clear();
                if (commandInProgress)
                {
                    commandOutcomeNotice = "The command outcome is unknown because the UPS connection was lost.";
                    StatusText.Text = commandOutcomeNotice;
                }
                else
                {
                    StatusText.Text = "UPS disconnected. Command buttons are disabled.";
                }
                UpdateCommandButtons();
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

        private void UpdateCommandListHeading(UPS_Device device)
        {
            string upsName = device == null || string.IsNullOrWhiteSpace(device.Name) ? "—" : device.Name;
            CommandListHeading.Text = string.Format("Instant commands supported on UPS [{0}]", upsName);
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
            var answer = MessageBox.Show(this,
                string.Format("Do you want to {0} on UPS '{1}'?", action, upsName),
                "Confirm UPS command", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            // The connection and advertised capability can change while the confirmation dialog is open.
            if (commandGeneration != connectionGeneration ||
                !ReferenceEquals(device, getDevice == null ? null : getDevice()) || !device.IsConnected ||
                !advertisedCommands.Contains(command))
            {
                StatusText.Text = "The UPS connection or advertised commands changed. Refresh the command list.";
                UpdateCommandButtons();
                return;
            }

            commandInProgress = true;
            commandOutcomeNotice = null;
            UpdateCommandButtons();
            StatusText.Text = string.Format("Sending '{0}' to UPS [{1}]...", command, upsName);
            try
            {
                Transaction transaction = await Task.Run(() => device.ExecuteUPSCommand(command));
                if (!IsCurrentRequest(device, commandGeneration))
                {
                    return;
                }

                if (command.StartsWith("test.battery.", StringComparison.Ordinal))
                {
                    StatusText.Text = string.Format(
                        "The NUT server accepted '{0}' for UPS [{1}].{2} Acceptance does not confirm that the battery test completed.",
                        command, upsName, TrackingAcknowledgement(transaction));
                }
                else
                {
                    StatusText.Text = string.Format("The NUT server accepted '{0}' for UPS [{1}].{2}",
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
                        StatusText.Text = "Command failed: " + ex.Message + " Refresh the command list before trying again.";
                    }
                    else
                    {
                        commandOutcomeNotice = "The command outcome is unknown because the UPS connection was lost.";
                        StatusText.Text = string.Format("Command failed while sending '{0}'. {1}", command,
                                                        commandOutcomeNotice);
                    }
                }
            }
            finally
            {
                commandInProgress = false;
                UpdateCommandButtons();
                StartPendingRefreshIfNeeded();
            }
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
        }
    }
}
