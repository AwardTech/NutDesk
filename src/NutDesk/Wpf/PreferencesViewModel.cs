using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using NutDesk.Notifications;
using NutDesk.Common;
using LogLvl = NutDesk.Common.LogLvl;
using static NutDesk.Common.AppGlobals;

namespace NutDesk.Wpf
{
    /// <summary>
    /// Lets the markup name the WinForms control whose caption or tooltip it wants -
    /// <c>{Binding Captions[Lbl_Port]}</c> - so the window needs no property per label and the key in the XAML
    /// is the very key a translator sees in Pref_Gui.resx.
    /// </summary>
    public sealed class PrefStringTable
    {
        private readonly Func<string, string> lookup;

        internal PrefStringTable(Func<string, string> lookup)
        {
            this.lookup = lookup;
        }

        public string this[string controlName] => lookup(controlName);
    }

    /// <summary>
    /// A text box the window checks while the user types.
    /// </summary>
    /// <remarks>
    /// The WinForms dialog only judged a field in its Validating event, when focus tried to leave it, and then
    /// held the focus hostage until the value was fixed. Here the verdict is live and never traps the user;
    /// the save refuses whatever is still wrong instead.
    /// </remarks>
    public abstract class InputField : INotifyPropertyChanged
    {
        private string text = string.Empty;
        private bool isEnabled = true;
        private bool isInvalid;
        private bool flagged;

        protected InputField(PreferencesViewModel owner, string name, int section)
        {
            Owner = owner;
            Name = name;
            Section = section;
            Tip = PrefStrings.Tip(name);
        }

        protected PreferencesViewModel Owner { get; }

        /// <summary>
        /// The WinForms control this field replaces: its resource keys, and the name the log lines have always
        /// used for it.
        /// </summary>
        internal string Name { get; }

        /// <summary>The navigation section that holds the field, so a refused save can show it.</summary>
        internal int Section { get; }

        public string Tip { get; }

        public string Text
        {
            get => text;
            set
            {
                value = value ?? string.Empty;
                if (value == text)
                {
                    return;
                }

                text = value;
                Raise();

                // Validating ran once per focus change; this runs once per keystroke. Logging only when the
                // verdict flips keeps the trace readable instead of burying it under one line per key.
                if (UpdateVerdict() && Owner.IsLoaded)
                {
                    LogVerdict(!isInvalid);
                }

                OnTextChanged();
                Owner.OnFieldEdited(this);
            }
        }

        /// <summary>
        /// Judges the current text again without it having changed, for a field whose rule depends on another
        /// setting - an email address only has to be one while email is switched on.
        /// </summary>
        internal void Revalidate()
        {
            UpdateVerdict();
        }

        /// <returns>True when the verdict flipped.</returns>
        private bool UpdateVerdict()
        {
            var wasInvalid = isInvalid;
            isInvalid = !Accepts(text);
            if (wasInvalid == isInvalid)
            {
                return false;
            }

            Raise(nameof(HasError));
            Raise(nameof(IsOutOfRange));
            return true;
        }

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (value == isEnabled)
                {
                    return;
                }

                // The WinForms handlers re-ran Number_Validating when they enabled a box, because its colour
                // could be stale. The verdict here is kept current on every change, so there is nothing to redo.
                isEnabled = value;
                Raise();

                // Validating kept focus in a bad box, so a box could never be switched off while holding an
                // invalid value. Nothing traps focus now: type 0 into the stop delay, then turn immediate stop
                // on, and the save backstop would refuse over a box the user can no longer edit. Put back the
                // last value it accepted instead.
                if (!value && isInvalid)
                {
                    RevertToLastValid();
                }
            }
        }

        /// <summary>The accepted span, shown inside the box while it holds something outside it.</summary>
        public virtual string RangeHint => null;

        public bool HasError => isInvalid || flagged;

        /// <summary>The field's own check failed, as opposed to being flagged as half of a bad pair.</summary>
        public bool IsOutOfRange => isInvalid;

        internal bool IsValid => !isInvalid;

        /// <summary>
        /// Set by the save-time calibration check. A field can be fine on its own and still be half of a
        /// minimum/maximum pair no reading can fall into.
        /// </summary>
        internal bool Flagged
        {
            get => flagged;
            set
            {
                if (value == flagged)
                {
                    return;
                }

                flagged = value;
                Raise(nameof(HasError));
            }
        }

        protected abstract bool Accepts(string candidate);

        protected virtual void LogVerdict(bool valid)
        {
        }

        protected virtual void OnTextChanged()
        {
        }

        /// <summary>What used to happen to the box when focus left it. Nothing, unless a field says otherwise.</summary>
        internal virtual void Normalize()
        {
        }

        /// <summary>Restores the last text the field accepted, if it has one.</summary>
        protected virtual void RevertToLastValid()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// A whole-number box with the bounds Number_Validating gave the control of the same name.
    /// </summary>
    public sealed class NumericField : InputField
    {
        internal NumericField(PreferencesViewModel owner, string name, int section, int min, int max)
            : base(owner, name, section)
        {
            Min = min;
            Max = max;
        }

        public int Min { get; }

        public int Max { get; }

        // Digits only, so it needs no translation - which is why it can say more than the red border alone.
        public override string RangeHint => string.Format(CultureInfo.CurrentCulture, "{0} – {1}", Min, Max);

        private string lastValidText;

        /// <summary>The same conversion Save_Params applied to the text box.</summary>
        internal int Value => Convert.ToInt32(Text);

        internal bool TryGetValue(out int value)
        {
            return int.TryParse(Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
        }

        protected override bool Accepts(string candidate)
        {
            // An empty box is unfinished rather than wrong: Number_Validating filled in the minimum as focus
            // left it, and Normalize still does. Painting it red while the user is retyping would only nag.
            if (candidate.Length == 0)
            {
                return true;
            }

            int result;
            return int.TryParse(candidate, NumberStyles.Integer, CultureInfo.CurrentCulture, out result)
                   && result >= Min && result <= Max;
        }

        protected override void LogVerdict(bool valid)
        {
            int result;
            if (!TryGetValue(out result))
            {
                // Number_Validating never logged text it could not read either; it only coloured the box.
                return;
            }

            if (valid)
            {
                LogFile.LogTracing(string.Format("Value of {0} for {1} is valid.", result, Name), LogLvl.LOG_DEBUG, Owner);
            }
            else
            {
                LogFile.LogTracing(string.Format("Value of {0} for {1} is invalid.", result, Name), LogLvl.LOG_ERROR, Owner);
            }
        }

        protected override void OnTextChanged()
        {
            if (IsValid && Text.Length > 0)
            {
                lastValidText = Text;
            }
        }

        protected override void RevertToLastValid()
        {
            if (lastValidText != null)
            {
                Text = lastValidText;
            }
        }

        internal override void Normalize()
        {
            if (Text.Length == 0)
            {
                Text = Min.ToString();
            }
        }

        internal void Load(int value)
        {
            Text = value.ToString();
        }
    }

    /// <summary>
    /// The NUT server address, accepted as Correct_IP_Validating accepted it: IPv4, IPv6 or a host name.
    /// </summary>
    public sealed class AddressField : InputField
    {
        // Verbatim from Correct_IP_Validating.
        private const string IPv4Pattern = @"^(25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)$";
        private const string IPv6Pattern = @"^\s*((([0-9A-Fa-f]{1,4}:){7}([0-9A-Fa-f]{1,4}|:))|(([0-9A-Fa-f]{1,4}:){6}(:[0-9A-Fa-f]{1,4}|((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3})|:))|(([0-9A-Fa-f]{1,4}:){5}(((:[0-9A-Fa-f]{1,4}){1,2})|:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3})|:))|(([0-9A-Fa-f]{1,4}:){4}(((:[0-9A-Fa-f]{1,4}){1,3})|((:[0-9A-Fa-f]{1,4})?:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){3}(((:[0-9A-Fa-f]{1,4}){1,4})|((:[0-9A-Fa-f]{1,4}){0,2}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){2}(((:[0-9A-Fa-f]{1,4}){1,5})|((:[0-9A-Fa-f]{1,4}){0,3}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){1}(((:[0-9A-Fa-f]{1,4}){1,6})|((:[0-9A-Fa-f]{1,4}){0,4}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(:(((:[0-9A-Fa-f]{1,4}){1,7})|((:[0-9A-Fa-f]{1,4}){0,5}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:)))(%.+)?\s*$";
        private const string FqdnPattern = @"^(?:(?!\d+\.|-)[a-zA-Z0-9_\-]{1,63}(?<!-)\.?)+(?:[a-zA-Z]{2,})$";

        // The host-name pattern nests a repeated group inside a repetition, so a long label that ends up not
        // matching backtracks exponentially. Behind a focus change that was a rare stall; checked on every
        // keystroke it would freeze the window, so each attempt is bounded and a timeout counts as no match.
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

        internal AddressField(PreferencesViewModel owner, string name, int section)
            : base(owner, name, section)
        {
        }

        protected override bool Accepts(string candidate)
        {
            return Classify(candidate) != null;
        }

        protected override void LogVerdict(bool valid)
        {
            if (valid)
            {
                // LOG_WARNING for a valid address is odd, but it is what the dialog has always written.
                LogFile.LogTracing(Classify(Text), LogLvl.LOG_WARNING, Owner);
            }
            else
            {
                LogFile.LogTracing("The Nut Host address is a invalid", LogLvl.LOG_ERROR, Owner);
            }
        }

        /// <summary>
        /// The trace line naming the kind of address, tried in Correct_IP_Validating's order; null when none fits.
        /// </summary>
        private static string Classify(string candidate)
        {
            if (Matches(candidate, IPv4Pattern))
            {
                return "The Nut Host address is a valid IPV4 address.";
            }

            if (Matches(candidate, IPv6Pattern))
            {
                return "The Nut Host address is a valid IPV6 address.";
            }

            if (Matches(candidate, FqdnPattern))
            {
                return "The Nut Host address is a valid FQDN address.";
            }

            return null;
        }

        private static bool Matches(string candidate, string pattern)
        {
            try
            {
                return Regex.IsMatch(candidate, pattern, RegexOptions.None, MatchTimeout);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The polling interval in seconds, with the NumericUpDown's bounds, step and one decimal place.
    /// </summary>
    public sealed class PollingField : InputField
    {
        // The NumericUpDown's Minimum and Increment from the designer, and its default Maximum.
        internal const decimal Minimum = 0.1m;
        internal const decimal Maximum = 100m;
        internal const decimal Increment = 0.1m;

        private decimal lastGood = Minimum;

        internal PollingField(PreferencesViewModel owner, string name, int section)
            : base(owner, name, section)
        {
        }

        public override string RangeHint =>
            string.Format(CultureInfo.CurrentCulture, "{0} – {1}", Format(Minimum), Format(Maximum));

        /// <summary>The value Save_Params read from pollingIntervalValue.Value.</summary>
        internal decimal Value => lastGood;

        protected override bool Accepts(string candidate)
        {
            if (candidate.Length == 0)
            {
                return true;
            }

            decimal value;
            return TryParse(candidate, out value) && value >= Minimum && value <= Maximum;
        }

        protected override void OnTextChanged()
        {
            decimal value;
            if (TryParse(Text, out value) && value >= Minimum && value <= Maximum)
            {
                lastGood = value;
            }
        }

        /// <summary>
        /// The NumericUpDown never held a value it could not use: when focus left it, it put back the previous
        /// value for text it could not read and clamped anything out of range. The same happens here.
        /// </summary>
        internal override void Normalize()
        {
            decimal typed;
            var readable = TryParse(Text, out typed);
            var next = readable ? Clamp(typed) : lastGood;

            lastGood = next;

            // Only rewrite text that changes the value: turning "5" into "5.0" is not an edit.
            if (!readable || typed != next)
            {
                Text = Format(next);
            }
        }

        internal void Step(int direction)
        {
            decimal typed;
            var current = TryParse(Text, out typed) ? typed : lastGood;
            var next = Clamp(current + direction * Increment);

            lastGood = next;
            Text = Format(next);
        }

        internal void Load(decimal value)
        {
            // Shown exactly as stored, and remembered exactly: the NumericUpDown rounded only what it displayed
            // and kept 1.05 in its Value, so saving an unrelated setting wrote 1050 ms back unchanged. Formatting
            // to one decimal and parsing that back would quietly turn 1050 into 1100.
            // A stored value outside the range is not clamped here either - the NumericUpDown refused to open
            // with one at all. It shows up red with its range beside it, and is only brought into range by
            // Normalize, visibly, when the user saves.
            Text = Format(value);
            lastGood = value;
        }

        private static decimal Clamp(decimal value)
        {
            return Math.Max(Minimum, Math.Min(Maximum, value));
        }

        // At least one decimal, like the NumericUpDown, but up to the millisecond the setting can hold, so a
        // value that is not a multiple of 100 ms is shown as it is rather than rounded.
        private static string Format(decimal value)
        {
            return value.ToString("0.0##", CultureInfo.CurrentCulture);
        }

        private static bool TryParse(string candidate, out decimal value)
        {
            return decimal.TryParse(candidate, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
        }
    }

    /// <summary>
    /// The sender address, or the list of recipients, of the email notifications.
    /// </summary>
    /// <remarks>
    /// Deliberately loose - something@something.tld without spaces. The SMTP server is the real judge, and the
    /// test button asks it; this only catches the typo that would otherwise surface as a notification that
    /// silently never arrived, weeks later, during the power cut it was meant to report.
    /// </remarks>
    public sealed class EmailAddressField : InputField
    {
        // Commas and semicolons are excluded because they are what separates recipients: "a@b.it;c@d.it" typed
        // into the sender box is two addresses, not one strange one.
        private static readonly Regex AddressPattern =
            new Regex(@"^[^@\s,;]+@[^@\s,;]+\.[^@\s,;]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

        private readonly bool allowsList;
        private readonly string usualTip;

        internal EmailAddressField(PreferencesViewModel owner, string name, int section, bool allowsList, string usualTip)
            : base(owner, name, section)
        {
            this.allowsList = allowsList;
            this.usualTip = usualTip;

            // The tooltip is where the reason for the red border is spelled out, so it has to follow the verdict.
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsOutOfRange))
                {
                    Raise(nameof(CurrentTip));
                }
            };
        }

        /// <summary>The field's usual help, replaced by what is wrong with it while it is wrong.</summary>
        public string CurrentTip => IsOutOfRange ? Properties.Resources.Pref_Email_InvalidAddress : usualTip;

        protected override bool Accepts(string candidate)
        {
            // An address nobody is going to write to cannot be wrong: with email off, a half-typed recipient
            // must neither glow red nor stand between the user and saving the rest of the window.
            if (!Owner.EmailEnabled)
            {
                return true;
            }

            if (!allowsList)
            {
                return IsAddress(candidate.Trim());
            }

            // Split by EmailAccount itself rather than a copy of its rule, so what passes here is exactly the
            // list the sender will get.
            var recipients = new EmailAccount { To = candidate }.Recipients;
            return recipients.Count > 0 && recipients.All(IsAddress);
        }

        protected override void LogVerdict(bool valid)
        {
            // The address itself stays out of the log: it is personal, and the verdict is all a trace needs.
            if (!valid)
            {
                LogFile.LogTracing(string.Format("The email address in {0} is invalid.", Name), LogLvl.LOG_DEBUG, Owner);
            }
        }

        private static bool IsAddress(string candidate)
        {
            try
            {
                return AddressPattern.IsMatch(candidate);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// One line of the notification matrix: an event and whether each channel reports it.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="NotificationKind"/> rather than written out eleven times in the
    /// markup, so an event appended to the enum turns up here with nothing else to touch but its title.
    /// </remarks>
    public sealed class NotificationEventRow : INotifyPropertyChanged
    {
        private readonly Action edited;
        private bool windows;
        private bool email;

        internal NotificationEventRow(NotificationKind kind, string label, Action edited)
        {
            Kind = kind;
            Label = label;
            this.edited = edited;

            // Both check boxes of a row would otherwise be announced by the same event title alone.
            WindowsName = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", label, Properties.Resources.Pref_Notif_ColumnWindows);
            EmailName = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", label, Properties.Resources.Pref_Notif_ColumnEmail);
        }

        internal NotificationKind Kind { get; }

        public string Label { get; }

        public string WindowsName { get; }

        public string EmailName { get; }

        public bool Windows
        {
            get => windows;
            set
            {
                if (value == windows)
                {
                    return;
                }

                windows = value;
                Raise();
                edited();
            }
        }

        public bool Email
        {
            get => email;
            set
            {
                if (value == email)
                {
                    return;
                }

                email = value;
                Raise();
                edited();
            }
        }

        /// <summary>Takes the stored subscriptions without counting as an edit.</summary>
        internal void Load(int toastEvents, int emailEvents)
        {
            windows = (toastEvents & (int)Kind) != 0;
            email = (emailEvents & (int)Kind) != 0;
            Raise(nameof(Windows));
            Raise(nameof(Email));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Backs the preferences window: every setting Pref_Gui edited, loaded, checked and saved the way Pref_Gui
    /// did it.
    /// </summary>
    /// <remarks>
    /// The WinForms dialog is the reference for everything here except the look. Where its behaviour lived in
    /// control events - Validating, CheckedChanged, TabControl.Selecting - it now lives in property setters, and
    /// each one says which handler it stands in for.
    /// </remarks>
    public sealed class PreferencesViewModel : INotifyPropertyChanged
    {
        internal const int SectionConnection = 0;
        internal const int SectionCalibration = 1;
        internal const int SectionMiscellaneous = 2;
        internal const int SectionShutdown = 3;
        internal const int SectionNotifications = 4;
        internal const int SectionEmail = 5;
        // Kept last: SelectedSection treats it as the upper bound.
        internal const int SectionUpdate = 6;

        private readonly Action saved;
        private readonly List<InputField> inputs = new List<InputField>();
        private readonly NumericField[][] calibrationPairs;

        // Pref_Gui's IsShowed: nothing counts as an edit until every stored setting is in place.
        private bool isLoaded;
        private bool prefsModified;
        private bool applyEnabled;
        private bool logControlsEnabled;
        private int selectedSection;

        private string upsName = string.Empty;
        private string login = string.Empty;
        private string password = string.Empty;
        private bool autoReconnect;

        private int freqInputIndex = -1;

        private bool minimizeToTray;
        private bool startMinimized;
        private bool closeToTray;
        private bool startWithWindows;
        private bool useLogFile;
        private int logLevelIndex = -1;

        private bool immediateStop;
        private bool followFsd;
        private int stopTypeIndex = -1;
        private bool extendTime;

        private bool checkAtStart;
        private int updateDelayIndex = -1;
        private int branchIndex = -1;

        private bool toastEnabled;
        private bool toastPersistCritical;
        // The subscriptions as stored, so bits for events this build does not know - written by a newer
        // NutDesk - survive being saved by this one.
        private int storedToastEvents;
        private int storedEmailEvents;

        private bool emailEnabled;
        private string smtpHost = string.Empty;
        private int smtpSecurityIndex = -1;
        private bool smtpAuth;
        private string smtpUser = string.Empty;
        private string smtpPassword = string.Empty;
        private string subjectPrefix = string.Empty;
        private int pendingEmails;

        private bool isSendingTest;
        private string testStatus;
        private bool testSucceeded;
        private bool testFailed;

        // The test email completes wherever the sender's task completes; its outcome is shown from here.
        private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

        public PreferencesViewModel(Action saved)
        {
            this.saved = saved;

            // The bounds are Number_Validating's switch, control by control.
            ServerAddress = Track(new AddressField(this, "Tb_Server_IP", SectionConnection));
            Port = Track(new NumericField(this, "Tb_Port", SectionConnection, 1, 65536));
            Polling = Track(new PollingField(this, "pollingIntervalValue", SectionConnection));

            InVMin = Track(new NumericField(this, "Tb_InV_Min", SectionCalibration, 0, 999));
            InVMax = Track(new NumericField(this, "Tb_InV_Max", SectionCalibration, 0, 999));
            InFMin = Track(new NumericField(this, "Tb_InF_Min", SectionCalibration, 0, 100));
            InFMax = Track(new NumericField(this, "Tb_InF_Max", SectionCalibration, 0, 100));
            OutVMin = Track(new NumericField(this, "Tb_OutV_Min", SectionCalibration, 0, 999));
            OutVMax = Track(new NumericField(this, "Tb_OutV_Max", SectionCalibration, 0, 999));
            BattVMin = Track(new NumericField(this, "Tb_BattV_Min", SectionCalibration, 0, 999));
            BattVMax = Track(new NumericField(this, "Tb_BattV_Max", SectionCalibration, 0, 999));

            BattLimitLoad = Track(new NumericField(this, "Tb_BattLimit_Load", SectionShutdown, 0, 100));
            BattLimitTime = Track(new NumericField(this, "Tb_BattLimit_Time", SectionShutdown, 0, 3600));
            // Min value has to be 1 as 0 can't be assigned to a timer interval (used in Shutdown_Gui).
            DelayStop = Track(new NumericField(this, "Tb_Delay_Stop", SectionShutdown, 1, 3600));
            GraceTime = Track(new NumericField(this, "Tb_GraceTime", SectionShutdown, 1, 3600));

            // Named after their settings: there is no WinForms control behind them for a log line to use.
            SmtpPort = Track(new NumericField(this, "NT_SmtpPort", SectionEmail, 1, 65535));
            EmailCooldown = Track(new NumericField(this, "NT_EmailCooldownMin", SectionEmail, 0, 1440));
            EmailFrom = Track(new EmailAddressField(this, "NT_EmailFrom", SectionEmail, false, null));
            EmailTo = Track(new EmailAddressField(this, "NT_EmailTo", SectionEmail, true, Properties.Resources.Pref_Email_ToTip));

            NotificationEvents = Enum.GetValues(typeof(NotificationKind))
                .Cast<NotificationKind>()
                .Where(kind => kind != NotificationKind.None)
                .Select(kind => new NotificationEventRow(kind, ResourceText("Notif_" + kind + "_Title", kind.ToString()), MarkModified))
                .ToList();

            calibrationPairs = new[]
            {
                new[] { InVMin, InVMax },
                new[] { InFMin, InFMax },
                new[] { OutVMin, OutVMax },
                new[] { BattVMin, BattVMax }
            };

            FreqInputItems = PrefStrings.Items("Cbx_Freq_Input", 2);
            LogLevelItems = PrefStrings.Items("Cbx_LogLevel", 4);
            StopTypeItems = PrefStrings.Items("Cbx_TypeStop", 3);
            UpdateDelayItems = PrefStrings.Items("Cbx_Delay_Verif", 3);
            BranchItems = PrefStrings.Items("Cbx_Branch_Update", 2);
            // In EmailSecurity order: the index is what NT_SmtpSecurity stores.
            SmtpSecurityItems = new[]
            {
                Properties.Resources.Pref_Email_SecurityAuto,
                Properties.Resources.Pref_Email_SecurityNone,
                Properties.Resources.Pref_Email_SecurityStartTls,
                Properties.Resources.Pref_Email_SecuritySsl
            };

            OkCommand = new RelayCommand(Ok);
            ApplyCommand = new RelayCommand(Apply);
            CancelCommand = new RelayCommand(Cancel);
            ViewLogCommand = new RelayCommand(ViewLog);
            DeleteLogCommand = new RelayCommand(DeleteLog);
            PollingUpCommand = new RelayCommand(() => Polling.Step(1));
            PollingDownCommand = new RelayCommand(() => Polling.Step(-1));
            SendTestEmailCommand = new RelayCommand(SendTestEmail);

            LoadSettings();
        }

        /// <summary>Asks the window to close: OK once nothing is left unsaved, or Cancel.</summary>
        internal event Action CloseRequested;

        /// <summary>
        /// True when the stored settings could not be shown. Pref_Gui closed itself in that case; the window
        /// does the same once it is up, because a window cannot be closed before it has been shown.
        /// </summary>
        internal bool LoadFailed { get; private set; }

        internal bool IsLoaded => isLoaded;

        #region Captions

        // Every string on the window comes out of Pref_Gui's resources; see PrefStrings for why.
        public PrefStringTable Captions { get; } = new PrefStringTable(PrefStrings.Text);

        public PrefStringTable Tips { get; } = new PrefStringTable(PrefStrings.Tip);

        /// <summary>
        /// The application's own resources, for the notification sections: they were written after Pref_Gui
        /// and so have no control in it to borrow a translation from.
        /// </summary>
        public PrefStringTable Strings { get; } = new PrefStringTable(key => ResourceText(key, key));

        public string Title => PrefStrings.Title;

        #endregion

        #region Navigation

        /// <summary>The section shown on the right, in the tab order the dialog always had.</summary>
        public int SelectedSection
        {
            get => selectedSection;
            set
            {
                if (value < SectionConnection || value > SectionUpdate || !SetField(ref selectedSection, value))
                {
                    return;
                }

                Raise(nameof(IsConnectionSection));
                Raise(nameof(IsCalibrationSection));
                Raise(nameof(IsMiscellaneousSection));
                Raise(nameof(IsShutdownSection));
                Raise(nameof(IsNotificationsSection));
                Raise(nameof(IsEmailSection));
                Raise(nameof(IsUpdateSection));

                // The outbox drains behind the window's back; count it again whenever someone may look at it.
                RefreshPendingEmail();

                // TabControl_Options_Selecting refreshed the log buttons on a tab change, since file logging
                // can have started or stopped behind the dialog's back. Doing it on every change also covers
                // arriving on the section, which is when the buttons are actually looked at.
                SetLogControlsStatus();
            }
        }

        public bool IsConnectionSection => selectedSection == SectionConnection;
        public bool IsCalibrationSection => selectedSection == SectionCalibration;
        public bool IsMiscellaneousSection => selectedSection == SectionMiscellaneous;
        public bool IsShutdownSection => selectedSection == SectionShutdown;
        public bool IsNotificationsSection => selectedSection == SectionNotifications;
        public bool IsEmailSection => selectedSection == SectionEmail;
        public bool IsUpdateSection => selectedSection == SectionUpdate;

        #endregion

        #region Connection

        public AddressField ServerAddress { get; }

        public NumericField Port { get; }

        public string UpsName
        {
            get => upsName;
            set => Edit(ref upsName, value ?? string.Empty);
        }

        public PollingField Polling { get; }

        public string Login
        {
            get => login;
            set => Edit(ref login, value ?? string.Empty);
        }

        /// <summary>
        /// Not bound: PasswordBox.Password is deliberately not a dependency property, so the window pushes the
        /// value in by hand.
        /// </summary>
        public string Password
        {
            get => password;
            set => Edit(ref password, value ?? string.Empty);
        }

        public bool AutoReconnect
        {
            get => autoReconnect;
            set => Edit(ref autoReconnect, value);
        }

        #endregion

        #region Calibration

        public NumericField InVMin { get; }
        public NumericField InVMax { get; }
        public NumericField InFMin { get; }
        public NumericField InFMax { get; }
        public NumericField OutVMin { get; }
        public NumericField OutVMax { get; }
        public NumericField BattVMin { get; }
        public NumericField BattVMax { get; }

        public string[] FreqInputItems { get; }

        public int FreqInputIndex
        {
            get => freqInputIndex;
            set => Edit(ref freqInputIndex, value);
        }

        #endregion

        #region Miscellaneous

        public bool MinimizeToTray
        {
            get => minimizeToTray;
            set
            {
                // CB_Systray_CheckedChanged: the two tray options mean nothing without the tray.
                if (Edit(ref minimizeToTray, value))
                {
                    Raise(nameof(TrayOptionsEnabled));
                }
            }
        }

        public bool TrayOptionsEnabled => minimizeToTray;

        public bool StartMinimized
        {
            get => startMinimized;
            set => Edit(ref startMinimized, value);
        }

        public bool CloseToTray
        {
            get => closeToTray;
            set => Edit(ref closeToTray, value);
        }

        public bool StartWithWindows
        {
            get => startWithWindows;
            set => Edit(ref startWithWindows, value);
        }

        public bool UseLogFile
        {
            get => useLogFile;
            set => Edit(ref useLogFile, value);
        }

        public string[] LogLevelItems { get; }

        public int LogLevelIndex
        {
            get => logLevelIndex;
            set => Edit(ref logLevelIndex, value);
        }

        /// <summary>
        /// Whether a log file exists to open or delete. It follows the logger, not the check box: ticking
        /// "create log file" does nothing until the preferences are saved.
        /// </summary>
        public bool LogControlsEnabled
        {
            get => logControlsEnabled;
            private set => SetField(ref logControlsEnabled, value);
        }

        #endregion

        #region Shutdown

        public NumericField BattLimitLoad { get; }

        public NumericField BattLimitTime { get; }

        public bool ImmediateStop
        {
            get => immediateStop;
            set
            {
                // Cb_ImmediateStop_CheckedChanged: an immediate stop has no delay to configure.
                if (Edit(ref immediateStop, value))
                {
                    DelayStop.IsEnabled = !immediateStop;
                }
            }
        }

        public bool FollowFsd
        {
            get => followFsd;
            set => Edit(ref followFsd, value);
        }

        public string[] StopTypeItems { get; }

        public int StopTypeIndex
        {
            get => stopTypeIndex;
            set => Edit(ref stopTypeIndex, value);
        }

        public NumericField DelayStop { get; }

        public bool ExtendTime
        {
            get => extendTime;
            set
            {
                // Cb_ExtendTime_CheckedChanged: the grace delay only applies when extending is allowed.
                if (Edit(ref extendTime, value))
                {
                    GraceTime.IsEnabled = extendTime;
                }
            }
        }

        public NumericField GraceTime { get; }

        #endregion

        #region Notifications

        public bool ToastEnabled
        {
            get => toastEnabled;
            set => Edit(ref toastEnabled, value);
        }

        public bool ToastPersistCritical
        {
            get => toastPersistCritical;
            set => Edit(ref toastPersistCritical, value);
        }

        /// <summary>One row per event, in the enum's order.</summary>
        public IReadOnlyList<NotificationEventRow> NotificationEvents { get; }

        #endregion

        #region Email

        public bool EmailEnabled
        {
            get => emailEnabled;
            set
            {
                if (Edit(ref emailEnabled, value))
                {
                    ApplyEmailEnabledState();
                }
            }
        }

        /// <summary>Shown under the event matrix, whose email column means nothing while email is off.</summary>
        public bool IsEmailOff => !emailEnabled;

        public string SmtpHost
        {
            get => smtpHost;
            set => Edit(ref smtpHost, value ?? string.Empty);
        }

        public NumericField SmtpPort { get; }

        public string[] SmtpSecurityItems { get; }

        public int SmtpSecurityIndex
        {
            get => smtpSecurityIndex;
            set
            {
                if (!Edit(ref smtpSecurityIndex, value) || !isLoaded)
                {
                    return;
                }

                // Nearly every provider pairs SSL/TLS with 465 and STARTTLS with 587, and a mismatch fails with
                // a TLS error that does not mention the port. Follow the choice, but only from the other
                // well-known port: a number the user typed on purpose is theirs.
                int port;
                if (!SmtpPort.TryGetValue(out port))
                {
                    return;
                }

                if (value == (int)EmailSecurity.SslOnConnect && port == 587)
                {
                    SmtpPort.Text = "465";
                }
                else if (value == (int)EmailSecurity.StartTls && port == 465)
                {
                    SmtpPort.Text = "587";
                }
            }
        }

        public bool SmtpAuth
        {
            get => smtpAuth;
            set => Edit(ref smtpAuth, value);
        }

        public string SmtpUser
        {
            get => smtpUser;
            set => Edit(ref smtpUser, value ?? string.Empty);
        }

        /// <summary>
        /// Not bound, for the same reason as <see cref="Password"/>: the window pushes the value in by hand.
        /// </summary>
        public string SmtpPassword
        {
            get => smtpPassword;
            set => Edit(ref smtpPassword, value ?? string.Empty);
        }

        public EmailAddressField EmailFrom { get; }

        public EmailAddressField EmailTo { get; }

        public string SubjectPrefix
        {
            get => subjectPrefix;
            set => Edit(ref subjectPrefix, value ?? string.Empty);
        }

        public NumericField EmailCooldown { get; }

        /// <summary>
        /// Messages still in the outbox. Worth saying next to the account settings, because a wrong password
        /// is usually why they are still there.
        /// </summary>
        public string PendingEmailNote =>
            pendingEmails > 0
                ? string.Format(CultureInfo.CurrentCulture, Properties.Resources.Pref_Email_Pending, pendingEmails)
                : null;

        public bool HasPendingEmail => pendingEmails > 0;

        /// <summary>False while a test email is on its way, so a second click cannot start another.</summary>
        public bool CanSendTestEmail => !isSendingTest;

        public string TestEmailStatus
        {
            get => testStatus;
            private set => SetField(ref testStatus, value);
        }

        public bool TestEmailSucceeded
        {
            get => testSucceeded;
            private set => SetField(ref testSucceeded, value);
        }

        public bool TestEmailFailed
        {
            get => testFailed;
            private set => SetField(ref testFailed, value);
        }

        /// <summary>
        /// What turning email on or off does to the fields under it, applied on a change and once after loading.
        /// </summary>
        private void ApplyEmailEnabledState()
        {
            Raise(nameof(IsEmailOff));

            // The addresses are only judged while email is on, so this switch alone can make them right or
            // wrong: turning email on paints an empty recipient box red, turning it off clears it.
            EmailFrom.Revalidate();
            EmailTo.Revalidate();
            EmailFrom.IsEnabled = emailEnabled;
            EmailTo.IsEnabled = emailEnabled;

            // As with the stop delay: a box switched off while holding something invalid goes back to the last
            // value it accepted, so it cannot block a save from where the user can no longer edit it.
            SmtpPort.IsEnabled = emailEnabled;
            EmailCooldown.IsEnabled = emailEnabled;
        }

        private void RefreshPendingEmail()
        {
            var count = 0;
            try
            {
                count = NotificationCenter.Current?.PendingEmailCount ?? 0;
            }
            catch (Exception ex)
            {
                // A count is not worth a broken window; without it the note simply does not show.
                LogFile.LogException(ex, this);
            }

            if (count == pendingEmails)
            {
                return;
            }

            pendingEmails = count;
            Raise(nameof(PendingEmailNote));
            Raise(nameof(HasPendingEmail));
        }

        #endregion

        #region Update

        /// <summary>
        /// UP_CheckAtStart, shown as "Check for updates automatically": whatever its name, the setting now covers the
        /// check a minute after startup and the ones that follow at <see cref="UpdateDelayIndex"/>'s interval while
        /// NutDesk runs (see MainForm's update timer). The caption comes from Pref_Gui's resources, like the others.
        /// </summary>
        public bool CheckAtStart
        {
            get => checkAtStart;
            set
            {
                // Cb_Update_At_Start_CheckedChanged.
                if (Edit(ref checkAtStart, value))
                {
                    Raise(nameof(UpdateDelayEnabled));
                }
            }
        }

        public bool UpdateDelayEnabled => checkAtStart;

        public string[] UpdateDelayItems { get; }

        public int UpdateDelayIndex
        {
            get => updateDelayIndex;
            set => Edit(ref updateDelayIndex, value);
        }

        public string[] BranchItems { get; }

        public int BranchIndex
        {
            get => branchIndex;
            set => Edit(ref branchIndex, value);
        }

        #endregion

        #region Commands

        public RelayCommand OkCommand { get; }

        public RelayCommand ApplyCommand { get; }

        public RelayCommand CancelCommand { get; }

        public RelayCommand ViewLogCommand { get; }

        public RelayCommand DeleteLogCommand { get; }

        public RelayCommand PollingUpCommand { get; }

        public RelayCommand PollingDownCommand { get; }

        public RelayCommand SendTestEmailCommand { get; }

        /// <summary>
        /// Btn_Apply.Enabled. Kept apart from the modified flag on purpose, as in the dialog: deleting the log
        /// file marks the preferences modified so OK re-saves them, without offering Apply for it.
        /// </summary>
        public bool ApplyEnabled
        {
            get => applyEnabled;
            private set => SetField(ref applyEnabled, value);
        }

        private void Ok()
        {
            if (prefsModified)
            {
                SaveParams();
                // SaveParams leaves prefsModified set when it refused to save. Closing anyway would throw
                // away every other edit the user made along with the highlighted fields they have to fix.
                if (prefsModified)
                {
                    return;
                }
            }

            CloseRequested?.Invoke();
        }

        private void Apply()
        {
            SaveParams();
            if (!prefsModified)
            {
                ApplyEnabled = false;
            }
        }

        internal void Cancel()
        {
            LogFile.LogTracing("Close Pref Gui from Button Cancel", LogLvl.LOG_DEBUG, this);
            CloseRequested?.Invoke();
        }

        /// <summary>
        /// Sends a test message with what is in the form right now, saved or not: the point is to find out
        /// whether the account works before trusting a power cut to it.
        /// </summary>
        /// <remarks>
        /// async void because it is a command handler, which is also why nothing may escape it. The window can
        /// be closed while the server is still answering; the outcome then lands on a model nobody shows any
        /// more, which is harmless.
        /// </remarks>
        private async void SendTestEmail()
        {
            if (isSendingTest)
            {
                return;
            }

            try
            {
                var account = BuildFormAccount();

                SetSendingTest(true);
                ShowTestOutcome(Properties.Resources.Pref_Email_TestSending, false, false);
                LogFile.LogTracing("Sending a test email.", LogLvl.LOG_DEBUG, this);

                string error;
                try
                {
                    // Awaited on the UI thread, so the window keeps painting while the server takes its time.
                    error = await NotificationCenter.SendTestEmailAsync(account);
                }
                catch (Exception ex)
                {
                    // The contract is to complete with a message rather than throw, but a fault here must still
                    // end as a line of text and never as a crash.
                    LogFile.LogException(ex, this);
                    error = ex.Message;
                }

                OnUiThread(() =>
                {
                    if (error == null)
                    {
                        LogFile.LogTracing("Test email sent.", LogLvl.LOG_NOTICE, this);
                        ShowTestOutcome(string.Format(CultureInfo.CurrentCulture, Properties.Resources.Pref_Email_TestOk,
                                                      string.Join(", ", account.Recipients)), true, false);
                    }
                    else
                    {
                        LogFile.LogTracing("Test email failed: " + error, LogLvl.LOG_WARNING, this);
                        ShowTestOutcome(string.Format(CultureInfo.CurrentCulture, Properties.Resources.Pref_Email_TestFailed, error), false, true);
                    }

                    SetSendingTest(false);
                    RefreshPendingEmail();
                });
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, this);
                OnUiThread(() => SetSendingTest(false));
            }
        }

        /// <summary>The account as the form stands, not as it was saved.</summary>
        private EmailAccount BuildFormAccount()
        {
            int port;
            return new EmailAccount
            {
                Host = smtpHost.Trim(),
                // An unreadable port is passed on as 0 rather than guessed: the sender refuses it with its own
                // message, and the red box already says which field is at fault.
                Port = SmtpPort.TryGetValue(out port) ? port : 0,
                Security = Enum.IsDefined(typeof(EmailSecurity), smtpSecurityIndex)
                    ? (EmailSecurity)smtpSecurityIndex
                    : EmailSecurity.Auto,
                RequiresAuth = smtpAuth,
                User = smtpUser,
                Password = smtpPassword,
                From = EmailFrom.Text.Trim(),
                To = EmailTo.Text,
                SubjectPrefix = subjectPrefix
            };
        }

        private void SetSendingTest(bool sending)
        {
            isSendingTest = sending;
            Raise(nameof(CanSendTestEmail));
        }

        private void ShowTestOutcome(string status, bool succeeded, bool failed)
        {
            TestEmailStatus = status;
            TestEmailSucceeded = succeeded;
            TestEmailFailed = failed;
        }

        /// <summary>
        /// The continuation of an await normally comes back on the UI thread, but that depends on the
        /// synchronization context the WinForms shell happened to leave installed; this does not.
        /// </summary>
        private void OnUiThread(Action action)
        {
            // Guarded inside the delegate as well: once posted, an exception would surface in the dispatcher
            // loop, far from any catch here.
            Action guarded = () =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, this);
                }
            };

            try
            {
                if (dispatcher.CheckAccess())
                {
                    guarded();
                }
                else
                {
                    dispatcher.BeginInvoke(guarded);
                }
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, this);
            }
        }

        #endregion

        #region Loading and saving

        private void LoadSettings()
        {
            try
            {
                isLoaded = false;
                ServerAddress.Text = Properties.Settings.Default.NUT_ServerAddress;
                Port.Load(Properties.Settings.Default.NUT_ServerPort);
                UpsName = Properties.Settings.Default.NUT_UPSName;
                Polling.Load(Properties.Settings.Default.NUT_PollIntervalMsec / 1000m);
                Login = Properties.Settings.Default.NUT_Username;
                Password = Properties.Settings.Default.NUT_Password;
                AutoReconnect = Properties.Settings.Default.NUT_AutoReconnect;
                InVMin.Load(Properties.Settings.Default.CAL_VoltInMin);
                InVMax.Load(Properties.Settings.Default.CAL_VoltInMax);
                FreqInputIndex = FindStringExact(FreqInputItems, Properties.Settings.Default.CAL_FreqInNom.ToString());
                InFMin.Load(Properties.Settings.Default.CAL_FreqInMin);
                InFMax.Load(Properties.Settings.Default.CAL_FreqInMax);
                OutVMin.Load(Properties.Settings.Default.CAL_VoltOutMin);
                OutVMax.Load(Properties.Settings.Default.CAL_VoltOutMax);
                BattVMin.Load(Properties.Settings.Default.CAL_BattVMin);
                BattVMax.Load(Properties.Settings.Default.CAL_BattVMax);
                MinimizeToTray = Properties.Settings.Default.MinimizeToTray;
                StartMinimized = Properties.Settings.Default.MinimizeOnStart;
                CloseToTray = Properties.Settings.Default.CloseToTray;
                StartWithWindows = Properties.Settings.Default.StartWithWindows;
                UseLogFile = Properties.Settings.Default.LG_LogToFile;
                LogLevelIndex = CheckedIndex(LogLevelItems, Properties.Settings.Default.LG_LogLevel);
                BattLimitLoad.Load(Properties.Settings.Default.PW_BattChrgFloor);
                BattLimitTime.Load(Properties.Settings.Default.PW_RuntimeFloor);
                ImmediateStop = Properties.Settings.Default.PW_Immediate;
                FollowFsd = Properties.Settings.Default.PW_RespectFSD;
                StopTypeIndex = CheckedIndex(StopTypeItems, Properties.Settings.Default.PW_StopType);
                DelayStop.Load(Properties.Settings.Default.PW_StopDelaySec);
                ExtendTime = Properties.Settings.Default.PW_UserExtendStopTimer;
                GraceTime.Load(Properties.Settings.Default.PW_ExtendDelaySec);
                CheckAtStart = Properties.Settings.Default.UP_CheckAtStart;
                UpdateDelayIndex = CheckedIndex(UpdateDelayItems, Properties.Settings.Default.UP_AutoChkDelay);
                BranchIndex = CheckedIndex(BranchItems, Properties.Settings.Default.UP_Branch);
                ToastEnabled = Properties.Settings.Default.NT_ToastEnabled;
                ToastPersistCritical = Properties.Settings.Default.NT_ToastPersistCritical;
                storedToastEvents = Properties.Settings.Default.NT_ToastEvents;
                storedEmailEvents = Properties.Settings.Default.NT_EmailEvents;
                foreach (var row in NotificationEvents)
                {
                    row.Load(storedToastEvents, storedEmailEvents);
                }

                SmtpHost = Properties.Settings.Default.NT_SmtpHost;
                SmtpPort.Load(Properties.Settings.Default.NT_SmtpPort);
                // Unlike the WinForms combos, an unknown value does not refuse to open the window: it is read
                // back as Automatic, exactly as the sender reads it, so the form shows what would really happen.
                SmtpSecurityIndex = Enum.IsDefined(typeof(EmailSecurity), Properties.Settings.Default.NT_SmtpSecurity)
                    ? Properties.Settings.Default.NT_SmtpSecurity
                    : (int)EmailSecurity.Auto;
                SmtpAuth = Properties.Settings.Default.NT_SmtpAuth;
                SmtpUser = Properties.Settings.Default.NT_SmtpUser;
                SmtpPassword = Properties.Settings.Default.NT_SmtpPassword;
                EmailFrom.Text = Properties.Settings.Default.NT_EmailFrom;
                EmailTo.Text = Properties.Settings.Default.NT_EmailTo;
                SubjectPrefix = Properties.Settings.Default.NT_EmailSubjectPrefix;
                EmailCooldown.Load(Properties.Settings.Default.NT_EmailCooldownMin);
                // Last of the email settings, because the address fields are judged by it.
                EmailEnabled = Properties.Settings.Default.NT_EmailEnabled;

                // The setters only react to a change, and a setting that matches the field's default is not
                // one - so the dependent states are applied once explicitly, as Pref_Gui_Shown did.
                Raise(nameof(TrayOptionsEnabled));
                DelayStop.IsEnabled = !immediateStop;
                GraceTime.IsEnabled = extendTime;
                Raise(nameof(UpdateDelayEnabled));
                ApplyEmailEnabledState();

                SetLogControlsStatus();
                RefreshPendingEmail();
                isLoaded = true;
                LogFile.LogTracing("Pref Gui Opened.", LogLvl.LOG_DEBUG, this);
            }
            catch (Exception Except)
            {
                isLoaded = false;
                LoadFailed = true;
                LogFile.LogTracing("Error on Opening Pref_Gui:" + Environment.NewLine + Except.ToString(), LogLvl.LOG_ERROR, this);
            }
        }

        private void SaveParams()
        {
            try
            {
                LogFile.LogTracing("Save Parameters.", LogLvl.LOG_DEBUG, this);

                // Clicking a WinForms button moved focus out of the field being edited, and Validating then put
                // the minimum into an empty box before Save_Params ever read it. Nothing guarantees that ordering
                // here, so it is done explicitly.
                foreach (var input in inputs)
                {
                    input.Normalize();
                }

                // The backstop for the live check. Validating refused to let focus leave a bad field, so
                // Save_Params could assume every box was in range; nothing traps the focus now, so the save has
                // to refuse instead. The field is already red with its range beside it - showing its section is
                // all the explanation it needs, and it needs no string the translators never saw.
                var outOfRange = inputs.FirstOrDefault(i => i is NumericField && IsEnforced(i) && !i.IsValid);
                if (outOfRange != null)
                {
                    LogFile.LogTracing(string.Format("Preferences not saved. The value of {0} is out of range.", outOfRange.Name), LogLvl.LOG_ERROR, this);
                    SelectedSection = outOfRange.Section;
                    // prefsModified stays set, exactly as for the calibration check below.
                    return;
                }

                // A bad address would not fail here but at the worst possible moment: the server would refuse the
                // message about a power cut, with nobody watching the log. Same treatment as a number out of
                // range - the box is already red, and its tooltip says why.
                var badAddress = inputs.FirstOrDefault(i => i is EmailAddressField && IsEnforced(i) && !i.IsValid);
                if (badAddress != null)
                {
                    LogFile.LogTracing(string.Format("Preferences not saved. The email address in {0} is invalid.", badAddress.Name), LogLvl.LOG_ERROR, this);
                    SelectedSection = badAddress.Section;
                    return;
                }

                // Each field is only range checked on its own, so an inverted or empty calibration range
                // (240/200, or 230/230) used to be saved happily and then left the matching gauge stuck at
                // empty forever. Refuse the whole save until every pair makes sense.
                bool InVRangeOk = IsCalibrationRangeValid(InVMin, InVMax);
                bool InFRangeOk = IsCalibrationRangeValid(InFMin, InFMax);
                bool OutVRangeOk = IsCalibrationRangeValid(OutVMin, OutVMax);
                bool BattVRangeOk = IsCalibrationRangeValid(BattVMin, BattVMax);

                if (!(InVRangeOk && InFRangeOk && OutVRangeOk && BattVRangeOk))
                {
                    LogFile.LogTracing("Preferences not saved. At least one calibration range is invalid.", LogLvl.LOG_ERROR, this);
                    SelectedSection = SectionCalibration;
                    // The WinForms box, not the WPF one: it takes the active window as its owner, so it stays
                    // on top of this window instead of possibly opening behind it.
                    System.Windows.Forms.MessageBox.Show(Properties.Resources.Pref_Gui_RangeErrorMessage, Properties.Resources.Pref_Gui_RangeErrorCaption,
                                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    // prefsModified stays set on purpose. Nothing was written, so Apply has to remain
                    // available and OK must not treat this as a completed save.
                    return;
                }

                Properties.Settings.Default.NUT_ServerAddress = ServerAddress.Text;
                Properties.Settings.Default.NUT_ServerPort = Port.Value;
                Properties.Settings.Default.NUT_UPSName = UpsName;
                Properties.Settings.Default.NUT_PollIntervalMsec = Convert.ToInt32(Polling.Value * 1000m);
                Properties.Settings.Default.NUT_Username = (SerializedProtectedString)Login;
                Properties.Settings.Default.NUT_Password = (SerializedProtectedString)Password;
                Properties.Settings.Default.NUT_AutoReconnect = AutoReconnect;
                Properties.Settings.Default.CAL_VoltInMin = InVMin.Value;
                Properties.Settings.Default.CAL_VoltInMax = InVMax.Value;
                Properties.Settings.Default.CAL_FreqInNom = Convert.ToInt32(FreqInputSelectedItem);
                Properties.Settings.Default.CAL_FreqInMin = InFMin.Value;
                Properties.Settings.Default.CAL_FreqInMax = InFMax.Value;
                Properties.Settings.Default.CAL_VoltOutMin = OutVMin.Value;
                Properties.Settings.Default.CAL_VoltOutMax = OutVMax.Value;
                Properties.Settings.Default.CAL_BattVMin = BattVMin.Value;
                Properties.Settings.Default.CAL_BattVMax = BattVMax.Value;
                Properties.Settings.Default.MinimizeToTray = MinimizeToTray;
                Properties.Settings.Default.MinimizeOnStart = StartMinimized;
                Properties.Settings.Default.CloseToTray = CloseToTray;
                Properties.Settings.Default.StartWithWindows = StartWithWindows;
                Properties.Settings.Default.LG_LogToFile = UseLogFile;
                Properties.Settings.Default.LG_LogLevel = LogLevelIndex;
                Properties.Settings.Default.PW_BattChrgFloor = BattLimitLoad.Value;
                Properties.Settings.Default.PW_RuntimeFloor = BattLimitTime.Value;
                Properties.Settings.Default.PW_Immediate = ImmediateStop;
                Properties.Settings.Default.PW_RespectFSD = FollowFsd;
                Properties.Settings.Default.PW_StopType = StopTypeIndex;
                Properties.Settings.Default.PW_StopDelaySec = DelayStop.Value;
                Properties.Settings.Default.PW_UserExtendStopTimer = ExtendTime;
                Properties.Settings.Default.PW_ExtendDelaySec = GraceTime.Value;
                Properties.Settings.Default.UP_CheckAtStart = CheckAtStart;
                Properties.Settings.Default.UP_AutoChkDelay = UpdateDelayIndex;
                Properties.Settings.Default.UP_Branch = BranchIndex;
                Properties.Settings.Default.NT_ToastEnabled = ToastEnabled;
                Properties.Settings.Default.NT_ToastPersistCritical = ToastPersistCritical;
                Properties.Settings.Default.NT_ToastEvents = ComposeEvents(storedToastEvents, row => row.Windows);
                Properties.Settings.Default.NT_EmailEnabled = EmailEnabled;
                Properties.Settings.Default.NT_EmailEvents = ComposeEvents(storedEmailEvents, row => row.Email);
                Properties.Settings.Default.NT_SmtpHost = SmtpHost.Trim();
                Properties.Settings.Default.NT_SmtpSecurity = SmtpSecurityIndex >= 0 ? SmtpSecurityIndex : (int)EmailSecurity.Auto;
                Properties.Settings.Default.NT_SmtpAuth = SmtpAuth;
                Properties.Settings.Default.NT_SmtpUser = SmtpUser;
                Properties.Settings.Default.NT_SmtpPassword = (SerializedProtectedString)SmtpPassword;
                Properties.Settings.Default.NT_EmailFrom = EmailFrom.Text.Trim();
                Properties.Settings.Default.NT_EmailTo = EmailTo.Text.Trim();
                Properties.Settings.Default.NT_EmailSubjectPrefix = SubjectPrefix;
                // With email off these two may hold anything, since nothing in a switched-off section may block
                // the save; whatever cannot be used simply leaves the stored value as it was.
                int smtpPort, cooldown;
                if (SmtpPort.IsValid && SmtpPort.TryGetValue(out smtpPort))
                {
                    Properties.Settings.Default.NT_SmtpPort = smtpPort;
                }

                if (EmailCooldown.IsValid && EmailCooldown.TryGetValue(out cooldown))
                {
                    Properties.Settings.Default.NT_EmailCooldownMin = cooldown;
                }

                Properties.Settings.Default.Save();

                storedToastEvents = Properties.Settings.Default.NT_ToastEvents;
                storedEmailEvents = Properties.Settings.Default.NT_EmailEvents;

                // Written on every save rather than only when missing, so that ticking the box again also repairs
                // an entry that still starts a copy of NutDesk from another folder.
                AutoStart.Apply(StartWithWindows);

                saved?.Invoke();

                SetLogControlsStatus();
                RefreshPendingEmail();
                LogFile.LogTracing("NutDesk Preferences Saved.", LogLvl.LOG_NOTICE, this, StrLog[(int)AppResxStr.STR_LOG_PREFS]);

                prefsModified = false;
            }
            catch (Exception e)
            {
                LogFile.LogTracing("Error when trying to save preferences.", LogLvl.LOG_ERROR, this);
                LogFile.LogException(e, this);
                // A stack trace is nothing an end user can act on, and an English literal caption cannot be
                // translated. The exception itself is already in the log, which is where the detail belongs.
                System.Windows.Forms.MessageBox.Show(Properties.Resources.Pref_Gui_SaveErrorMessage, Properties.Resources.Pref_Gui_SaveErrorCaption,
                                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Check a calibration min/max pair, flagging both fields when it fails.
        /// </summary>
        /// <returns>True when the minimum is strictly lower than the maximum.</returns>
        private bool IsCalibrationRangeValid(NumericField MinBox, NumericField MaxBox)
        {
            // A single field's range check only ever sees one box at a time and so cannot tell that the two
            // together describe a span no reading can fall into, which is what empties the gauge.
            if (IsPairOrdered(MinBox, MaxBox))
            {
                MinBox.Flagged = false;
                MaxBox.Flagged = false;
                return true;
            }

            LogFile.LogTracing(string.Format("Calibration range {0}/{1} is invalid. The minimum has to be lower than the maximum.", MinBox.Name, MaxBox.Name), LogLvl.LOG_ERROR, this);
            MinBox.Flagged = true;
            MaxBox.Flagged = true;
            return false;
        }

        /// <summary>
        /// Whether a field's verdict may refuse the save. Nothing in the Email section may while email is off:
        /// the user switched the feature off precisely so as not to have to fill it in.
        /// </summary>
        private bool IsEnforced(InputField field)
        {
            return field.Section != SectionEmail || emailEnabled;
        }

        /// <summary>
        /// The ticked events as one flags value, keeping any bit a newer NutDesk stored for an event this one has
        /// no row for - an older build must not quietly unsubscribe the user from it.
        /// </summary>
        private int ComposeEvents(int stored, Func<NotificationEventRow, bool> ticked)
        {
            var known = 0;
            var chosen = 0;
            foreach (var row in NotificationEvents)
            {
                known |= (int)row.Kind;
                if (ticked(row))
                {
                    chosen |= (int)row.Kind;
                }
            }

            return (stored & ~known) | chosen;
        }

        /// <summary>
        /// A string from the application's resources. Like PrefStrings, it may not throw: a missing string
        /// costs a label its wording, never the window.
        /// </summary>
        private static string ResourceText(string key, string fallback)
        {
            try
            {
                return Properties.Resources.ResourceManager.GetString(key, Properties.Resources.Culture) ?? fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static bool IsPairOrdered(NumericField min, NumericField max)
        {
            int minValue, maxValue;
            return min.TryGetValue(out minValue) && max.TryGetValue(out maxValue) && minValue < maxValue;
        }

        /// <summary>Cbx_Freq_Input.SelectedItem, which is what Save_Params converted - the text, not the index.</summary>
        private object FreqInputSelectedItem =>
            freqInputIndex >= 0 && freqInputIndex < FreqInputItems.Length ? FreqInputItems[freqInputIndex] : null;

        /// <summary>ComboBox.FindStringExact: case-insensitive, -1 when nothing matches.</summary>
        private static int FindStringExact(string[] items, string text)
        {
            return Array.FindIndex(items, item => string.Equals(item, text, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// A stored index as the WinForms combo accepted it. The combo threw for anything out of range and the
        /// dialog refused to open; a WPF combo would quietly show nothing, and the next save would write the
        /// blank back. Throwing keeps the old outcome.
        /// </summary>
        private static int CheckedIndex(string[] items, int index)
        {
            if (index < -1 || index >= items.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Stored selection is outside the list.");
            }

            return index;
        }

        #endregion

        #region Change tracking

        /// <summary>
        /// Event_Ctrl_Value_Changed, for the text boxes. Also clears a calibration pair's highlight as soon as
        /// the pair makes sense again, as the dialog did on its next check - but never raises one mid-typing:
        /// on the way from 230 to 260 the maximum briefly reads 2.
        /// </summary>
        internal void OnFieldEdited(InputField field)
        {
            MarkModified();

            if (!field.Flagged)
            {
                return;
            }

            foreach (var pair in calibrationPairs)
            {
                if ((pair[0] == field || pair[1] == field) && IsPairOrdered(pair[0], pair[1]))
                {
                    pair[0].Flagged = false;
                    pair[1].Flagged = false;
                }
            }
        }

        private void MarkModified()
        {
            if (isLoaded)
            {
                prefsModified = true;
                ApplyEnabled = true;
            }
        }

        private T Track<T>(T field) where T : InputField
        {
            inputs.Add(field);
            return field;
        }

        /// <summary>SetField for a setting: a real change is also an edit.</summary>
        private bool Edit<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!SetField(ref field, value, propertyName))
            {
                return false;
            }

            MarkModified();
            return true;
        }

        #endregion

        #region Logging controls

        private void ViewLog()
        {
            LogFile.LogTracing("User clicked ViewLog button.", LogLvl.LOG_DEBUG, this);
            try
            {
                Process.Start(LogFile.LogFilePath);
                LogFile.LogTracing("Opened UI window to log location.", LogLvl.LOG_NOTICE, this);
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, this);
                SetLogControlsStatus();
            }
        }

        private void DeleteLog()
        {
            LogFile.LogTracing("User clicked DeleteLog button.", LogLvl.LOG_DEBUG, this);

            try
            {
                LogFile.DeleteLogFile();
                prefsModified = true; // Will help reinitialize log file later if user still wants it.
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, this);
            }

            SetLogControlsStatus();
        }

        /// <summary>
        /// Enable or disable controls to view and delete log data if it's available.
        /// </summary>
        private void SetLogControlsStatus()
        {
            LogControlsEnabled = LogFile.IsWritingToFile;
            Raise(nameof(LogFileLocation));
        }

        /// <summary>
        /// Where the log is being written, beside the two buttons that act on it. The WinForms dialog showed two
        /// bare icons; a path needs no translation and says which file they mean.
        /// </summary>
        public string LogFileLocation
        {
            get
            {
                try
                {
                    return LogFile.IsWritingToFile ? LogFile.LogFilePath : null;
                }
                catch (InvalidOperationException)
                {
                    // File logging can stop between the two calls.
                    return null;
                }
            }
        }

        #endregion

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
