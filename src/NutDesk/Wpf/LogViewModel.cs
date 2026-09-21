using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Data;
using NutDesk.Common;

namespace NutDesk.Wpf
{
    /// <summary>
    /// One row in the log window.
    /// </summary>
    public sealed class LogEntry
    {
        internal LogEntry(LogEvent source)
        {
            Source = source;
        }

        internal LogEvent Source { get; }

        public long Sequence => Source.Sequence;

        public LogLvl Level => Source.Level;

        public string Time => Source.Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

        public string Sender => Source.Sender;

        /// <summary>
        /// The translated wording when there is one, because that is what the event means to the user; the
        /// raw trace message otherwise.
        /// </summary>
        private string RawText => Source.DisplayMessage ?? Source.Message;

        /// <summary>
        /// The row's single-line rendering. Exception dumps and the notify-icon text carry embedded
        /// newlines, and a TextBlock honours those even with wrapping off - a handful of such events would
        /// otherwise push every other row off the screen. The full text stays in <see cref="Detail"/>.
        /// </summary>
        public string Text => Flatten(RawText);

        public bool IsUserFacing => Source.IsUserFacing;

        /// <summary>
        /// The tooltip: the untranslated message behind a user-facing event, or the untruncated text of an
        /// event that had to be flattened for the row. Null when the row already shows everything.
        /// </summary>
        public string Detail
        {
            get
            {
                if (Source.IsUserFacing)
                {
                    return Source.Message;
                }

                return RawText != null && RawText != Text ? RawText : null;
            }
        }

        public string LevelText => LogViewModel.LevelName(Source.Level);

        /// <summary>The clipboard gets the original text: it is headed for a bug report, not for scanning.</summary>
        internal string ToClipboardLine()
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}  {1,-7}  {2}", Time, LevelText, RawText);
        }

        internal bool Matches(string needle)
        {
            return Contains(RawText, needle) || Contains(Sender, needle);
        }

        private static bool Contains(string haystack, string needle)
        {
            return haystack != null && haystack.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0)
            {
                return text;
            }

            var sb = new StringBuilder(text.Length);
            var lastWasSpace = false;

            foreach (var c in text)
            {
                var isSpace = c == '\r' || c == '\n' || c == '\t' || c == ' ';

                if (isSpace)
                {
                    if (!lastWasSpace && sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                }
                else
                {
                    sb.Append(c);
                }

                lastWasSpace = isSpace;
            }

            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Backs the event-log window: the live history, the filters over it, and the two actions.
    /// </summary>
    /// <remarks>
    /// The window used to be a hidden combo box on the main form, which the WPF dashboard replaced and
    /// nothing put back. This restores the view and gives it what the combo never had: severity, source and
    /// a filter, because the whole point is to see at a glance when the connection dropped or the UPS went
    /// on battery.
    /// </remarks>
    public sealed class LogViewModel : INotifyPropertyChanged
    {
        // How many of the rows are user-facing: the rows are bounded per kind exactly like the logger's own
        // history, so the view holds everything the logger remembers and the routine traffic of a window left
        // open cannot push a power cut out of it either.
        private int userFacingRows;

        private readonly Logger logger;
        private readonly ObservableCollection<LogEntry> entries = new ObservableCollection<LogEntry>();
        private readonly HashSet<LogLvl> levels = new HashSet<LogLvl>
        {
            LogLvl.LOG_NOTICE, LogLvl.LOG_WARNING, LogLvl.LOG_ERROR
        };

        private string searchText = string.Empty;
        private bool onlyImportant = true;
        private bool autoScroll = true;

        public LogViewModel(Logger logger)
        {
            this.logger = logger;

            foreach (var ev in logger.RecentEvents)
            {
                entries.Add(new LogEntry(ev));

                if (ev.IsUserFacing)
                {
                    userFacingRows += 1;
                }
            }

            View = CollectionViewSource.GetDefaultView(entries);
            View.Filter = o => Accepts((LogEntry)o);
            // Newest first: the reason to open this window is almost always "what just happened?".
            View.SortDescriptions.Add(new SortDescription(nameof(LogEntry.Sequence), ListSortDirection.Descending));

            CopyCommand = new RelayCommand(CopyVisible);
            OpenLogFileCommand = new RelayCommand(OpenLogFolder);
        }

        public ICollectionView View { get; }

        public RelayCommand CopyCommand { get; }

        public RelayCommand OpenLogFileCommand { get; }

        #region Captions

        // Read once here rather than from the XAML, so the window has no compile-time dependency on the
        // resource designer and the whole surface is localized from one place.
        public string SearchPlaceholder => Properties.Resources.LogWindow_SearchPlaceholder;
        public string OnlyImportantCaption => Properties.Resources.LogWindow_OnlyImportant;
        public string OnlyImportantTip => Properties.Resources.LogWindow_OnlyImportantTip;
        public string AutoScrollCaption => Properties.Resources.LogWindow_AutoScroll;
        public string CopyCaption => Properties.Resources.LogWindow_Copy;
        public string CopyTip => Properties.Resources.LogWindow_CopyTip;
        public string OpenFileCaption => Properties.Resources.LogWindow_OpenFile;
        public string ColumnTime => Properties.Resources.LogWindow_ColumnTime;
        public string ColumnLevel => Properties.Resources.LogWindow_ColumnLevel;
        public string ColumnSource => Properties.Resources.LogWindow_ColumnSource;
        public string ColumnMessage => Properties.Resources.LogWindow_ColumnMessage;
        public string LevelNotice => Properties.Resources.LogWindow_LevelNotice;
        public string LevelWarning => Properties.Resources.LogWindow_LevelWarning;
        public string LevelError => Properties.Resources.LogWindow_LevelError;
        public string LevelDebug => Properties.Resources.LogWindow_LevelDebug;
        public string EmptyText => Properties.Resources.LogWindow_Empty;

        #endregion

        #region Filters

        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetField(ref searchText, value ?? string.Empty))
                {
                    Refresh();
                }
            }
        }

        /// <summary>
        /// When set, only events that carry a translated, user-facing wording are listed. That is the subset
        /// the user actually asked to see - connections, power source changes, failures - and it is on by
        /// default; the count line makes it obvious that more events exist behind it.
        /// </summary>
        public bool OnlyImportant
        {
            get => onlyImportant;
            set
            {
                if (SetField(ref onlyImportant, value))
                {
                    Refresh();
                }
            }
        }

        public bool AutoScroll
        {
            get => autoScroll;
            set => SetField(ref autoScroll, value);
        }

        public bool ShowNotice
        {
            get => levels.Contains(LogLvl.LOG_NOTICE);
            set => SetLevel(LogLvl.LOG_NOTICE, value);
        }

        public bool ShowWarning
        {
            get => levels.Contains(LogLvl.LOG_WARNING);
            set => SetLevel(LogLvl.LOG_WARNING, value);
        }

        public bool ShowError
        {
            get => levels.Contains(LogLvl.LOG_ERROR);
            set => SetLevel(LogLvl.LOG_ERROR, value);
        }

        public bool ShowDebug
        {
            get => levels.Contains(LogLvl.LOG_DEBUG);
            set => SetLevel(LogLvl.LOG_DEBUG, value);
        }

        private void SetLevel(LogLvl level, bool wanted, [CallerMemberName] string propertyName = null)
        {
            if (wanted == levels.Contains(level))
            {
                return;
            }

            if (wanted)
            {
                levels.Add(level);
            }
            else
            {
                levels.Remove(level);
            }

            Raise(propertyName);
            Refresh();
        }

        private bool Accepts(LogEntry entry)
        {
            if (!levels.Contains(entry.Level))
            {
                return false;
            }

            if (onlyImportant && !entry.IsUserFacing)
            {
                return false;
            }

            return searchText.Length == 0 || entry.Matches(searchText);
        }

        #endregion

        #region Live updates

        /// <summary>
        /// Appends one event. The window calls this from the UI thread; the logger raises its event on
        /// whichever thread did the logging, so the marshalling happens there.
        /// </summary>
        internal void Append(LogEvent ev)
        {
            var entry = new LogEntry(ev);
            entries.Add(entry);

            if (entry.IsUserFacing)
            {
                userFacingRows += 1;
            }

            TrimKind(true, Logger.USER_FACING_HISTORY_LIMIT);
            TrimKind(false, Logger.EVENT_HISTORY_LIMIT);

            RaiseCounts();
        }

        /// <summary>Drops the oldest rows of one kind until that kind is back within its bound.</summary>
        private void TrimKind(bool userFacing, int limit)
        {
            var count = userFacing ? userFacingRows : entries.Count - userFacingRows;

            for (var i = 0; count > limit && i < entries.Count;)
            {
                if (entries[i].IsUserFacing == userFacing)
                {
                    entries.RemoveAt(i);
                    count -= 1;

                    if (userFacing)
                    {
                        userFacingRows -= 1;
                    }
                }
                else
                {
                    i += 1;
                }
            }
        }

        public int TotalCount => entries.Count;

        public int VisibleCount => View.Cast<object>().Count();

        public string CountText =>
            string.Format(CultureInfo.CurrentCulture, Properties.Resources.LogWindow_Count, VisibleCount, TotalCount);

        public bool IsEmpty => VisibleCount == 0;

        private void Refresh()
        {
            View.Refresh();
            RaiseCounts();
        }

        private void RaiseCounts()
        {
            Raise(nameof(TotalCount));
            Raise(nameof(VisibleCount));
            Raise(nameof(CountText));
            Raise(nameof(IsEmpty));
        }

        #endregion

        #region Commands

        public bool CanOpenLogFile => logger.IsWritingToFile;

        public string OpenLogFileTip => logger.IsWritingToFile
            ? Properties.Resources.LogWindow_OpenFileTip
            : Properties.Resources.LogWindow_OpenFileDisabled;

        internal void RefreshFileState()
        {
            Raise(nameof(CanOpenLogFile));
            Raise(nameof(OpenLogFileTip));
        }

        private void CopyVisible()
        {
            var sb = new StringBuilder();

            foreach (LogEntry entry in View)
            {
                sb.AppendLine(entry.ToClipboardLine());
            }

            try
            {
                System.Windows.Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                // Another process can hold the clipboard open; that is not worth an error dialog.
                logger.LogTracing("Unable to copy the log to the clipboard.", LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
            }
        }

        private void OpenLogFolder()
        {
            if (!logger.IsWritingToFile)
            {
                return;
            }

            try
            {
                // Select the file rather than opening it: the user may want yesterday's file too, and this
                // avoids handing a .log to whatever happens to be registered for the extension.
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + logger.LogFilePath + "\"");
            }
            catch (Exception ex)
            {
                logger.LogTracing("Unable to open the log file location.", LogLvl.LOG_ERROR, this);
                logger.LogException(ex, this);
            }
        }

        #endregion

        internal static string LevelName(LogLvl level)
        {
            switch (level)
            {
                case LogLvl.LOG_WARNING:
                    return Properties.Resources.LogWindow_LevelWarning;
                case LogLvl.LOG_ERROR:
                    return Properties.Resources.LogWindow_LevelError;
                case LogLvl.LOG_DEBUG:
                    return Properties.Resources.LogWindow_LevelDebug;
                default:
                    return Properties.Resources.LogWindow_LevelNotice;
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            Raise(propertyName);
            return true;
        }

        #endregion
    }
}
