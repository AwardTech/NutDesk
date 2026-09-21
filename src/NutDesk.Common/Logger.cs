using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace NutDesk.Common
{
    /// <summary>
    /// Determines how often a new log file is started. Mirrors the VB runtime's option of the same name; the
    /// VB type itself cannot be used because this project must not reference Microsoft.VisualBasic.
    /// </summary>
    public enum LogFileCreationScheduleOption
    {
        None,
        Daily,
        Weekly
    }

    public class Logger
    {
        #region Constants/Shared
        private const LogFileCreationScheduleOption LOG_FILE_CREATION_SCHEDULE = LogFileCreationScheduleOption.Daily;
        private const string SUBDIRECTORY = "Logs";
        public const int MAX_DISPLAYED_LOGS = 50;

        /// <summary>
        /// How many routine events - those without a user-facing wording: polling, protocol traffic, debug
        /// detail - are kept in memory for the log window and the crash report. Well above the crash report's
        /// window (<see cref="MaxEvents"/>), and still small enough that the cost is a few hundred kilobytes.
        /// </summary>
        public const int EVENT_HISTORY_LIMIT = 1000;

        /// <summary>
        /// How many user-facing events are kept, counted apart from the routine ones. A short polling interval
        /// produces a thousand routine events in a minute or two, and with a single bound they pushed a power cut
        /// out of the history before anyone opened the log window; the events the window exists for now have an
        /// allowance of their own, which covers weeks of normal use.
        /// </summary>
        public const int USER_FACING_HISTORY_LIMIT = 500;

        /// <summary>
        /// Base name of the log files written by <see cref="RollingFileLogWriter"/>. The VB runtime's
        /// FileLogTraceListener derived this from the application's product name; it is spelled out here so the
        /// existing "NutDesk-yyyy-MM-dd.log" files keep being appended to.
        /// </summary>
        private const string LOG_FILE_BASE_NAME = "NutDesk";

#if DEBUG
        private static readonly DateTimeFormatInfo DEFAULT_DATETIMEFORMAT = DateTimeFormatInfo.InvariantInfo;
#else
        private static readonly DateTimeFormatInfo DEFAULT_DATETIMEFORMAT = DateTimeFormatInfo.CurrentInfo;
#endif

        private readonly TraceEventCache TEventCache = new TraceEventCache();

        /// <summary>
        /// Guards every mutable field below. Connection work now runs on a thread pool thread, so the event list,
        /// the displayed-log queue and the file listener are written from more than one thread.
        /// </summary>
        private readonly object logLock = new object();
        #endregion

        #region Private/backing values

        private RollingFileLogWriter LogFile;
        private List<LogEvent> LastEventsList = new List<LogEvent>();
        private long _eventSequence = 0;
        private int _userFacingCount = 0; // How many of LastEventsList are user-facing.
        private Queue<string> _displayedLogs = new Queue<string>(MAX_DISPLAYED_LOGS);
        private int _displayedLogsCounter = 0; // As incrementing when a new displayed log is added.
        private DateTimeFormatInfo _DateTimeFormatInfo = DEFAULT_DATETIMEFORMAT;

        #endregion

        public LogLvl LogLevelValue;

        public event Action<string> DisplayedLogsLineAdded;
        public event Action<string> DisplayedLogsTrimmed;

        /// <summary>
        /// Raised for every recorded event, so a live view can append without polling.
        /// </summary>
        public event Action<LogEvent> EventLogged;

        #region Properties

        private int _MaxEvents = 200;

        /// <summary>
        /// How many of the most recent events <see cref="LastEvents"/> hands to the crash report.
        /// </summary>
        /// <remarks>
        /// This used to be the capacity of the history itself, so lowering it discarded events for good. The
        /// history is now bounded separately by <see cref="EVENT_HISTORY_LIMIT"/> - the log window wants more
        /// depth than a bug report does - and this only decides how much of it the report carries.
        /// </remarks>
        public int MaxEvents
        {
            get => _MaxEvents;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxEvents), "Maximum number of events cannot be negative.");
                }

                // The value used to be validated and then thrown away, so this property could never be changed.
                lock (logLock)
                {
                    _MaxEvents = value;
                }
            }
        }

        /// <summary>
        /// Friendly log messages that are intended to be displayed to the user.
        /// </summary>
        /// <returns></returns>
        public Queue<string> DisplayedLogs => _displayedLogs;

        /// <summary>
        /// A snapshot of the recent events, as the formatted lines the crash report has always contained.
        /// Deliberately a copy: the crash reporter reverses and serialises this while the connection worker
        /// may still be appending to the live list.
        /// </summary>
        public List<object> LastEvents
        {
            get
            {
                lock (logLock)
                {
                    var start = Math.Max(0, LastEventsList.Count - _MaxEvents);
                    var snapshot = new List<object>(LastEventsList.Count - start);

                    for (var i = start; i < LastEventsList.Count; i++)
                    {
                        snapshot.Add(LastEventsList[i].FormattedLine);
                    }

                    return snapshot;
                }
            }
        }

        /// <summary>
        /// A snapshot of the structured event history, oldest first. Also a copy, for the same reason as
        /// <see cref="LastEvents"/>: the log window enumerates it while other threads are still logging.
        /// </summary>
        public List<LogEvent> RecentEvents
        {
            get
            {
                lock (logLock)
                {
                    return new List<LogEvent>(LastEventsList);
                }
            }
        }

        /// <summary>
        /// Check status of the log file writer, as well as start or stop logging to a file.
        /// Events continue to be recorded to memory regardless.
        /// </summary>
        /// <returns>True when the <see cref="LogFile"/> object is instantiated, false if not.</returns>
        public bool IsWritingToFile
        {
            get => LogFile != null;
            set
            {
                if (value != IsWritingToFile)
                {
                    if (value)
                    {
                        InitializeLogFile();
                    }
                    else
                    {
                        TerminateLogFile();
                    }
                }
            }
        }

        /// <summary>
        /// Get the full path of the <see cref="LogFile"/> object's log file.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        public string LogFilePath
        {
            get
            {
                if (IsWritingToFile)
                {
                    return LogFile.FullLogFileName;
                }
                else
                {
                    throw new InvalidOperationException("Log file has not been created.");
                }
            }
        }

        public DateTimeFormatInfo DateTimeFormatInfo
        {
            get => _DateTimeFormatInfo;
            set => _DateTimeFormatInfo = value;
        }


        #endregion

        public Logger(LogLvl LogLevel)
        {
            LogLevelValue = LogLevel;
        }

        #region Log file management

        /// <summary>
        /// Instantiates a new <see cref="RollingFileLogWriter"/> with a location that's determined by a pre-defined
        /// algorithm. The <see cref="LastEvents"/> buffer is written into the file before synchronizing with write calls.
        /// </summary>
        private void InitializeLogFile()
        {
            LogTracing("InitializeLogFile called...", LogLvl.LOG_DEBUG, this);

            try
            {
                LogFile = new RollingFileLogWriter(Path.Combine(AppGlobals.DataDirectory, SUBDIRECTORY),
                                                   LOG_FILE_BASE_NAME)
                {
                    Append = true,
                    AutoFlush = true,
                    LogFileCreationSchedule = LOG_FILE_CREATION_SCHEDULE
                };

                // Calling the trace sub will also cause it to try to write to the log file, validating it.
                LogTracing($"Attempt init log file: { LogFilePath }", LogLvl.LOG_NOTICE, this);
            }
            catch (Exception ex)
            {
                TerminateLogFile();
                LogException(ex, this);
            }

            if (LastEventsList.Count > 0)
            {
                // Fill new file with the LastEventsList buffer
                LogFile.WriteLine("==== History of " + LastEventsList.Count + " previous events ====");

                for (int index = 0; index <= LastEventsList.Count - 1; index++)
                {
                    LogFile.WriteLine(string.Format("[{0}] {1}", index + 1, LastEventsList[index]));
                }
            }

            LogFile.WriteLine("==== Begin Live Log ====" + Environment.NewLine);
        }

        /// <summary>
        /// End logging to the <see cref="LogFile"/> by writing a terminating line to it, then closing and dereferencing it.
        /// </summary>
        /// <exception cref="InvalidOperationException">The LogFile object is already Nothing and file logging is disabled.</exception>
        ///
        public void TerminateLogFile()
        {
            if (IsWritingToFile)
            {
                LogTracing("Terminating log file.", LogLvl.LOG_NOTICE, this);
                LogFile.Close();
                LogFile.Dispose();
                LogFile = null;
            }
            else
            {
                var invOpExcp = new InvalidOperationException("Unable to terminate log file - already disabled.");
                LogException(invOpExcp, this);
                throw invOpExcp;
            }
        }

        /// <summary>
        /// Disable logging and delete the current file. May throw an exception while deleting the file,
        /// even if file logging was enabled.
        /// </summary>
        /// <exception cref="InvalidOperationException">LogFile object is Nothing.</exception>
        public void DeleteLogFile()
        {
            if (IsWritingToFile)
            {
                var fileLocation = LogFile.FullLogFileName;
                TerminateLogFile();
                File.Delete(fileLocation);
                LogTracing("Log file has been deleted.", LogLvl.LOG_NOTICE, this);
            }
            else
            {
                var invOpExcp = new InvalidOperationException("File logging is disabled, unable to delete log file.");
                LogException(invOpExcp, this);
                throw invOpExcp;
            }
        }

        #endregion

        /// <summary>
        /// Write the <paramref name="message"/> to the Debug tracer is debugging, into the <see cref="LastEventsList" />
        /// for report generating, to the <see cref="LogFile"/> if appropriate, and notify any listeners if
        /// <paramref name="LogToDisplay"/> is specified.
        /// </summary>
        /// <param name="message">The raw information that needs to be recorded.</param>
        /// <param name="LvlError">The severity of the message.</param>
        /// <param name="sender">What generated this message.</param>
        /// <param name="LogToDisplay">A user-friendly, translated string to be shown.</param>
        public void LogTracing(string message, LogLvl LvlError, object sender, string LogToDisplay = null)
        {
            // Captured once so the stored event and the formatted line cannot disagree about when it happened.
            var now = DateTime.Now;
            var senderName = SenderNameOf(sender);
            var FinalMsg = FormatLogLine(message, now, senderName);

            // Always write log messages to the attached debug messages window.
            Trace.WriteLine(FinalMsg);

            string trimmedLine = null;
            string addedLine = null;
            LogEvent recordedEvent;

            lock (logLock)
            {
                // Create Event in EventList in case of crash for generate Report. Each kind of event is bounded
                // on its own, so the routine traffic can only ever evict routine events.
                var userFacing = LogToDisplay != null;
                var full = userFacing
                    ? _userFacingCount >= USER_FACING_HISTORY_LIMIT
                    : LastEventsList.Count - _userFacingCount >= EVENT_HISTORY_LIMIT;

                if (full)
                {
                    LastEventsList.RemoveAt(LastEventsList.FindIndex(e => e.IsUserFacing == userFacing));
                }
                else if (userFacing)
                {
                    _userFacingCount += 1;
                }

                _eventSequence += 1;
                recordedEvent = new LogEvent(_eventSequence, now, LvlError, senderName, message, LogToDisplay, FinalMsg);
                LastEventsList.Add(recordedEvent);

                // Send message to log file if enabled
                if (IsWritingToFile && LogLevelValue >= LvlError)
                {
                    LogFile.WriteLine(FinalMsg);
                }

                // Insert new log message for display to the user, and prune old ones.
                if (LogToDisplay != null)
                {
                    if (_displayedLogs.Count >= MAX_DISPLAYED_LOGS)
                    {
                        trimmedLine = _displayedLogs.Dequeue();
                    }

                    _displayedLogsCounter += 1;
                    // The VB source read: String.Format(Now, "General Date"). With Option Strict off that
                    // converted Now into a string with VB's "General Date" layout and used it as the format
                    // string; the "General Date" argument was never substituted because the result holds no
                    // placeholders. FormatGeneralDate reproduces exactly that string.
                    addedLine = string.Format("[{0}][{1}] {2}", _displayedLogsCounter,
                                              FormatGeneralDate(DateTime.Now), LogToDisplay);
                    _displayedLogs.Enqueue(addedLine);
                }
            }

            // Raised outside the lock: subscribers marshal onto the UI thread, and holding the lock across that
            // would let a logging call from the UI thread deadlock against one from the connection worker.
            EventLogged?.Invoke(recordedEvent);

            if (trimmedLine != null)
            {
                LogTracing($"Removed log from displayed logs collection: { trimmedLine }", LogLvl.LOG_DEBUG, this);
                DisplayedLogsTrimmed?.Invoke(trimmedLine);
            }

            if (addedLine != null)
            {
                LogTracing("Added new line to displayed logs collection: " + addedLine, LogLvl.LOG_DEBUG, this);
                DisplayedLogsLineAdded?.Invoke(addedLine);
            }
        }

        public void LogException(Exception ex, object sender)
        {
            var sb = new StringBuilder();
            sb.AppendLine(ex.GetType().ToString() + " thrown in " + ex.Source);
            sb.AppendLine("Message: " + ex.Message);
            sb.AppendLine(ex.StackTrace);

            LogTracing(sb.ToString(), LogLvl.LOG_ERROR, sender);

            if (ex.InnerException != null)
            {
                LogTracing("Inner exception present:", LogLvl.LOG_ERROR, sender);
                LogException(ex.InnerException, ex);
            }

            LogTracing("Exception report complete.", LogLvl.LOG_NOTICE, this);
        }

        private static string SenderNameOf(object sender)
        {
            return sender == null ? "Nothing" : sender.GetType().Name;
        }

        private string FormatLogLine(string message, DateTime timestamp, string senderName)
        {
            var Pid = TEventCache.ProcessId;

            return string.Format("{0} [{1}, {2}]: {3}", timestamp.ToString(_DateTimeFormatInfo), Pid, senderName, message);
        }

        /// <summary>
        /// Reproduces VB's conversion of a Date to a String ("General Date"): the date alone when the time is
        /// midnight, the time alone when the date is 1/1/0001, and otherwise the current culture's general
        /// date/long time pattern.
        /// </summary>
        private static string FormatGeneralDate(DateTime value)
        {
            if (value.TimeOfDay.Ticks == 0)
            {
                return value.ToString("d", CultureInfo.CurrentCulture);
            }

            if (value.Date == DateTime.MinValue.Date)
            {
                return value.ToString("T", CultureInfo.CurrentCulture);
            }

            return value.ToString("G", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Stand-in for the VB runtime's FileLogTraceListener, which cannot be used here (no dependency on
        /// Microsoft.VisualBasic is allowed.) It keeps the behaviour the rest of this class relies on: a log file
        /// named "&lt;base name&gt;-yyyy-MM-dd.log" inside the given directory, a new file each day, appending to
        /// an existing file and flushing after every line. The file is opened lazily on the first write or on the
        /// first read of <see cref="FullLogFileName"/>, so an unusable directory surfaces its exception there -
        /// exactly where the VB listener raised it.
        /// </summary>
        private sealed class RollingFileLogWriter : IDisposable
        {
            private const string FILE_EXTENSION = ".log";
            private const int MAX_NAME_COLLISION_ATTEMPTS = 100;

            private readonly string customLocation;
            private readonly string baseFileName;

            private StreamWriter writer;
            private string fullLogFileName;
            private string currentPeriodStamp;

            public RollingFileLogWriter(string customLocation, string baseFileName)
            {
                this.customLocation = customLocation;
                this.baseFileName = baseFileName;
            }

            /// <summary>Append to an existing file rather than truncating it.</summary>
            public bool Append { get; set; } = true;

            /// <summary>Flush the underlying stream after every written line.</summary>
            public bool AutoFlush { get; set; } = true;

            /// <summary>How often a new file is started.</summary>
            public LogFileCreationScheduleOption LogFileCreationSchedule { get; set; } = LogFileCreationScheduleOption.None;

            /// <summary>
            /// The full path of the file currently being written to. Reading this opens the file if it is not
            /// already open, which is how the caller validates that the location is usable.
            /// </summary>
            public string FullLogFileName
            {
                get
                {
                    EnsureStreamIsOpen();
                    return fullLogFileName;
                }
            }

            public void WriteLine(string message)
            {
                EnsureStreamIsOpen();
                writer.WriteLine(message);

                if (AutoFlush)
                {
                    writer.Flush();
                }
            }

            public void Close()
            {
                if (writer != null)
                {
                    writer.Flush();
                    writer.Dispose();
                    writer = null;
                }
            }

            public void Dispose()
            {
                Close();
            }

            private void EnsureStreamIsOpen()
            {
                var stamp = GetPeriodStamp(DateTime.Now);

                if (writer != null)
                {
                    if (stamp == currentPeriodStamp)
                    {
                        return;
                    }

                    // The schedule has rolled over into a new period - start a new file.
                    Close();
                }

                Directory.CreateDirectory(customLocation);

                var desiredName = baseFileName + stamp;
                IOException lastException = null;

                for (int attempt = 0; attempt < MAX_NAME_COLLISION_ATTEMPTS; attempt++)
                {
                    // A file already held open by another instance gets a numeric suffix, like the VB listener did.
                    var candidate = Path.Combine(customLocation,
                        (attempt == 0 ? desiredName : $"{desiredName}-{attempt}") + FILE_EXTENSION);

                    try
                    {
                        writer = new StreamWriter(candidate, Append, new UTF8Encoding(true));
                        fullLogFileName = candidate;
                        currentPeriodStamp = stamp;
                        return;
                    }
                    catch (IOException ex)
                    {
                        lastException = ex;
                    }
                }

                throw lastException;
            }

            private string GetPeriodStamp(DateTime now)
            {
                switch (LogFileCreationSchedule)
                {
                    case LogFileCreationScheduleOption.Daily:
                        return "-" + now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                    case LogFileCreationScheduleOption.Weekly:
                        return "-" + now.AddDays(-(int)now.DayOfWeek).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                    default:
                        return string.Empty;
                }
            }
        }
    }
}
