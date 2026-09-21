using System;

namespace NutDesk.Notifications
{
    /// <summary>
    /// The events NutDesk can notify about. A flags enum because each channel stores the set it is subscribed to
    /// as one integer setting (NT_ToastEvents, NT_EmailEvents).
    /// </summary>
    /// <remarks>
    /// The values are persisted in user.config: never renumber an existing member, only append.
    /// </remarks>
    [Flags]
    public enum NotificationKind
    {
        None = 0,

        /// <summary>The UPS switched to battery (OB became active), or was already on battery when NutDesk connected.</summary>
        PowerLost = 1 << 0,

        /// <summary>The UPS is back on mains power (OL became active) after running on battery.</summary>
        PowerRestored = 1 << 1,

        /// <summary>The UPS reports a low battery (LB became active).</summary>
        LowBattery = 1 << 2,

        /// <summary>NutDesk started shutting this computer down, because of the battery limits or an FSD from the server.</summary>
        ShutdownStarted = 1 << 3,

        /// <summary>A shutdown countdown was cancelled because power came back.</summary>
        ShutdownCancelled = 1 << 4,

        /// <summary>The connection to the NUT server broke unexpectedly. Not raised for a disconnection the user asked for.</summary>
        ConnectionLost = 1 << 5,

        /// <summary>NutDesk reached the NUT server again after <see cref="ConnectionLost"/>. Not raised for the first connection.</summary>
        ConnectionRestored = 1 << 6,

        /// <summary>NutDesk gave up reconnecting: configuration problem, or automatic reconnection turned off.</summary>
        ReconnectAbandoned = 1 << 7,

        /// <summary>The UPS reports that its battery needs replacing (RB became active).</summary>
        BatteryReplace = 1 << 8,

        /// <summary>The UPS reports an overload (OVER became active).</summary>
        Overload = 1 << 9,

        /// <summary>A newer NutDesk release is available.</summary>
        UpdateAvailable = 1 << 10,
    }
}
