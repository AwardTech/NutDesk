using System;

namespace NutDesk.Common
{
    /// <summary>
    /// One entry in the log, kept as structured data rather than as a pre-formatted line.
    /// </summary>
    /// <remarks>
    /// The logger used to keep only the formatted string, which was enough for the crash report but threw
    /// away the severity and the timestamp - so nothing could filter or sort the history afterwards. The log
    /// window needs both, and it also needs to tell an ordinary trace line apart from the translated,
    /// user-facing message that accompanies the events people actually care about (lost connection, running
    /// on battery, reconnected).
    /// </remarks>
    public sealed class LogEvent
    {
        internal LogEvent(long sequence, DateTime timestamp, LogLvl level, string sender, string message,
                          string displayMessage, string formattedLine)
        {
            Sequence = sequence;
            Timestamp = timestamp;
            Level = level;
            Sender = sender;
            Message = message;
            DisplayMessage = displayMessage;
            FormattedLine = formattedLine;
        }

        /// <summary>
        /// Monotonic counter assigned when the event was recorded. Two events can share a timestamp, so this
        /// is what gives the history a stable order.
        /// </summary>
        public long Sequence { get; }

        public DateTime Timestamp { get; }

        public LogLvl Level { get; }

        /// <summary>Type name of whatever produced the message, or "Nothing" when it was logged statically.</summary>
        public string Sender { get; }

        /// <summary>The raw, untranslated message.</summary>
        public string Message { get; }

        /// <summary>
        /// The translated, user-facing wording, or null for an ordinary trace line. Only a small fraction of
        /// log calls supply one, and those are exactly the events worth showing at a glance.
        /// </summary>
        public string DisplayMessage { get; }

        /// <summary>True when this event carries a <see cref="DisplayMessage"/>.</summary>
        public bool IsUserFacing => DisplayMessage != null;

        /// <summary>The line as written to the log file, kept so the crash report is unchanged.</summary>
        public string FormattedLine { get; }

        public override string ToString()
        {
            return FormattedLine;
        }
    }
}
