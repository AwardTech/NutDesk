using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.Integration;
using NutDesk.Common;

namespace NutDesk.Wpf
{
    /// <summary>Displays commands advertised by the active NUT connection.</summary>
    public partial class UPSCommandsWindow : Window
    {
        private readonly Func<UPS_Device> getDevice;
        private bool paletteIsDark;

        public UPSCommandsWindow(Func<UPS_Device> getDevice, bool isDark)
        {
            this.getDevice = getDevice;
            InitializeComponent();
            paletteIsDark = isDark;
            Resources.MergedDictionaries.Insert(0, Palette.Build(isDark));
            ElementHost.EnableModelessKeyboardInterop(this);
            Loaded += async (sender, e) => await LoadCommandsAsync();
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

        private async Task LoadCommandsAsync()
        {
            RefreshButton.IsEnabled = false;
            StatusText.Text = "Retrieving command list...";
            CommandList.Items.Clear();

            try
            {
                UPS_Device device = getDevice == null ? null : getDevice();
                if (device == null)
                {
                    throw new InvalidOperationException("Connect to a UPS before retrieving its command list.");
                }

                List<string> commands = await Task.Run(() => device.GetUPSCommands());
                foreach (string command in commands)
                {
                    CommandList.Items.Add(command);
                }

                StatusText.Text = commands.Count == 0
                    ? "The server did not advertise any instant commands."
                    : string.Format("{0} command(s) advertised.", commands.Count);
            }
            catch (Exception ex)
            {
                // Query diagnostics redact PASSWORD commands at the socket boundary. This operation only issues
                // LIST CMD, so its exception message contains no credential material.
                StatusText.Text = "Unable to retrieve the command list: " + ex.Message;
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }
    }
}
