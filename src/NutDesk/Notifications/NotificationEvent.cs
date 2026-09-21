using System;
using System.Globalization;

namespace NutDesk.Notifications
{
    /// <summary>
    /// One thing worth telling the user about, fully rendered at the moment it happened.
    /// </summary>
    /// <remarks>
    /// Title and body are rendered once, when the event occurs, rather than by each channel when it gets round to
    /// it: an email may leave hours later, and by then the device it describes may be gone or showing different
    /// values.
    /// </remarks>
    internal sealed class NotificationEvent
    {
        internal NotificationEvent(NotificationKind kind, UpsSnapshot snapshot, string title, string body)
        {
            Kind = kind;
            Snapshot = snapshot;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }

        internal NotificationKind Kind { get; }

        internal UpsSnapshot Snapshot { get; }

        internal string Title { get; }

        internal string Body { get; }

        internal DateTime EventUtc => Snapshot.TimestampUtc;
    }

    /// <summary>
    /// What each kind means to the channels: which kinds cancel each other out, which are urgent, and which toasts
    /// replace which.
    /// </summary>
    internal static class NotificationKinds
    {
        /// <summary>
        /// The events the user must not miss because the computer is about to lose power. These are the ones that
        /// may stay on screen until dismissed.
        /// </summary>
        internal static bool IsCritical(NotificationKind kind)
        {
            return kind == NotificationKind.PowerLost
                || kind == NotificationKind.LowBattery
                || kind == NotificationKind.ShutdownStarted;
        }

        /// <summary>
        /// The kind that undoes this one, or None. A pending message of one kind becomes a lie as soon as its
        /// opposite has happened after it.
        /// </summary>
        internal static NotificationKind Opposite(NotificationKind kind)
        {
            switch (kind)
            {
                case NotificationKind.PowerLost: return NotificationKind.PowerRestored;
                case NotificationKind.PowerRestored: return NotificationKind.PowerLost;
                case NotificationKind.ConnectionLost: return NotificationKind.ConnectionRestored;
                case NotificationKind.ConnectionRestored: return NotificationKind.ConnectionLost;
                case NotificationKind.ShutdownStarted: return NotificationKind.ShutdownCancelled;
                case NotificationKind.ShutdownCancelled: return NotificationKind.ShutdownStarted;
                default: return NotificationKind.None;
            }
        }

        /// <summary>
        /// The toast group a kind belongs to. Toasts of one topic describe the same piece of state, so the newest
        /// replaces the older one instead of leaving "power failure" stacked under "power restored".
        /// </summary>
        internal static string Topic(NotificationKind kind)
        {
            switch (kind)
            {
                case NotificationKind.PowerLost:
                case NotificationKind.PowerRestored:
                case NotificationKind.LowBattery:
                    return "power";

                case NotificationKind.ShutdownStarted:
                case NotificationKind.ShutdownCancelled:
                    return "shutdown";

                case NotificationKind.ConnectionLost:
                case NotificationKind.ConnectionRestored:
                case NotificationKind.ReconnectAbandoned:
                    return "connection";

                default:
                    // Kept short: Windows limits a toast tag to 64 characters, 16 on the oldest Windows 10 builds.
                    return kind.ToString().ToLowerInvariant();
            }
        }
    }

    /// <summary>
    /// Looks resource strings up in an explicit culture.
    /// </summary>
    /// <remarks>
    /// Events can arrive on thread-pool threads and emails are finished on the outbox worker, whose culture is not
    /// necessarily the one the user sees. Every lookup therefore names the culture captured on the UI thread.
    /// </remarks>
    internal static class NotificationText
    {
        internal static string Get(string name, CultureInfo culture)
        {
            try
            {
                return Properties.Resources.ResourceManager.GetString(name, culture) ?? string.Empty;
            }
            catch (Exception)
            {
                // A missing satellite assembly must cost a word, not a notification.
                return string.Empty;
            }
        }

        /// <summary>
        /// string.Format that cannot throw: a translation with a broken placeholder still produces its text.
        /// </summary>
        internal static string Format(CultureInfo culture, string template, params object[] args)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            try
            {
                return string.Format(culture, template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        internal static string Title(NotificationKind kind, CultureInfo culture)
        {
            return Get("Notif_" + kind + "_Title", culture);
        }

        internal static string Body(NotificationKind kind, CultureInfo culture)
        {
            return Get("Notif_" + kind + "_Body", culture);
        }
    }
}
