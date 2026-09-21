using System;
using System.Collections.Generic;
using System.Globalization;
using NutDesk.Common;

namespace NutDesk.Notifications
{
    /// <summary>
    /// The state of the UPS at the moment an event happened, copied out of the device.
    /// </summary>
    /// <remarks>
    /// Copied rather than referenced: the device keeps polling and may be disposed and replaced long before a
    /// delayed email about it is written out. A null value means "not known", never zero.
    /// </remarks>
    internal sealed class UpsSnapshot
    {
        private UpsSnapshot()
        {
        }

        internal DateTime TimestampUtc { get; private set; }

        internal string Computer { get; private set; }

        /// <summary>"host:port", or null when no server is configured.</summary>
        internal string Server { get; private set; }

        /// <summary>The UPS name on the NUT server, or null when there is no device.</summary>
        internal string UpsName { get; private set; }

        internal UPS_States? Status { get; private set; }

        internal double? ChargePercent { get; private set; }

        internal double? RuntimeSeconds { get; private set; }

        internal double? LoadPercent { get; private set; }

        internal double? InputVoltage { get; private set; }

        /// <summary>
        /// Copies what the device currently knows. Safe with a null device and from any thread; never throws.
        /// </summary>
        internal static UpsSnapshot Capture(UPS_Device device)
        {
            var snapshot = new UpsSnapshot
            {
                TimestampUtc = DateTime.UtcNow,
                Computer = SafeMachineName(),
            };

            if (device == null)
            {
                return snapshot;
            }

            try
            {
                var config = device.Nut_Config;

                if (config != null)
                {
                    snapshot.UpsName = string.IsNullOrWhiteSpace(config.UPSName) ? null : config.UPSName;
                    snapshot.Server = FormatServer(config.Host, config.Port);
                }

                // Readings are only meaningful from a live connection that has completed at least one poll. Right
                // after a (re)connection the device holds a fresh UPSData whose values are all still zero, and
                // after a lost connection the last values describe the past: both are reported as unknown rather
                // than as a flat battery.
                var values = device.IsConnected ? device.UPS_Datas?.UPS_Value : null;

                if (values != null && values.UPS_Status != UPS_States.None)
                {
                    snapshot.Status = values.UPS_Status;
                    snapshot.ChargePercent = InRange(values.Batt_Charge, 0, 100);
                    snapshot.RuntimeSeconds = InRange(values.Batt_Runtime, 0, double.MaxValue);
                    snapshot.LoadPercent = InRange(values.Load, 0, double.MaxValue);
                    snapshot.InputVoltage = InRange(values.Input_Voltage, double.Epsilon, double.MaxValue);
                }
            }
            catch (Exception)
            {
                // The device may be torn down on the UI thread while an event is being handled elsewhere. Whatever
                // was copied so far is still true; the rest stays unknown.
            }

            return snapshot;
        }

        #region Formatting

        internal static string FormatServer(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return null;
            }

            host = host.Trim();

            // An IPv6 literal needs brackets, or its own colons swallow the port.
            if (host.IndexOf(':') >= 0 && !host.StartsWith("[", StringComparison.Ordinal))
            {
                host = "[" + host + "]";
            }

            return host + ":" + port.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// "45 s", "12 min", "1 h 05 min". Only numbers and unit symbols, so it reads the same in every language
        /// without needing a translation of its own.
        /// </summary>
        internal static string FormatDuration(TimeSpan duration, CultureInfo culture)
        {
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            var totalSeconds = (long)Math.Round(duration.TotalSeconds);

            if (totalSeconds < 60)
            {
                return totalSeconds.ToString(culture) + " s";
            }

            var totalMinutes = totalSeconds / 60;

            if (totalMinutes < 60)
            {
                return totalMinutes.ToString(culture) + " min";
            }

            // Hours keep counting past a day: "27 h 05 min" needs no word for "day" in any language.
            return (totalMinutes / 60).ToString(culture) + " h " + (totalMinutes % 60).ToString("00", culture) + " min";
        }

        internal string ServerText(string unknown) => Server ?? unknown;

        internal string UpsNameText(string unknown) => UpsName ?? unknown;

        /// <summary>The number alone, because the message templates put the percent sign themselves.</summary>
        internal string ChargeNumber(CultureInfo culture, string unknown) =>
            ChargePercent.HasValue ? ChargePercent.Value.ToString("0", culture) : unknown;

        internal string LoadNumber(CultureInfo culture, string unknown) =>
            LoadPercent.HasValue ? LoadPercent.Value.ToString("0", culture) : unknown;

        internal string RuntimeText(CultureInfo culture, string unknown) =>
            RuntimeSeconds.HasValue ? FormatDuration(TimeSpan.FromSeconds(RuntimeSeconds.Value), culture) : unknown;

        internal string InputVoltageText(CultureInfo culture, string unknown) =>
            InputVoltage.HasValue ? InputVoltage.Value.ToString("0.#", culture) + " V" : unknown;

        /// <summary>
        /// The active statuses as the NUT server spells them ("OB LB"), which is what anyone searching the NUT
        /// documentation or the server logs will recognise.
        /// </summary>
        internal string StatusText(string unknown)
        {
            if (!Status.HasValue)
            {
                return unknown;
            }

            var tokens = new List<string>();

            foreach (UPS_States flag in Enum.GetValues(typeof(UPS_States)))
            {
                if (flag != UPS_States.None && (Status.Value & flag) == flag)
                {
                    tokens.Add(flag.ToString());
                }
            }

            return tokens.Count > 0 ? string.Join(" ", tokens) : unknown;
        }

        #endregion

        private static double? InRange(double value, double min, double max)
        {
            // The device uses -1 for "not reported", and a runtime it had to estimate can come out as NaN or
            // infinity when the load reads zero.
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
            {
                return null;
            }

            return value;
        }

        private static string SafeMachineName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
