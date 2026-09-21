using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NutDesk.Common;

namespace NutDesk.Notifications
{
    /// <summary>
    /// Turns what the UPS device reports into notifications - Windows toasts and emails - according to the
    /// preferences the user has at that moment.
    /// </summary>
    /// <remarks>
    /// The shell creates a new UPS_Device on every manual connection, so anything that must outlive one device - an
    /// outage in progress, whether the UPS was on battery - is kept here rather than per device.
    ///
    /// UPS_Device raises its events on the UI thread, but nothing here relies on it: every entry point may be called
    /// from any thread, and none of them may throw or block. A notification that fails is logged and forgotten; it
    /// must never be the reason monitoring stops.
    /// </remarks>
    internal sealed class NotificationCenter : IDisposable
    {
        private static readonly object currentSync = new object();

        /// <summary>The whole test, connection included. The user is watching a "Sending..." label meanwhile.</summary>
        private static readonly TimeSpan TEST_EMAIL_TIMEOUT = TimeSpan.FromSeconds(20);

        /// <summary>The instance the shell created, or null before it exists or after it was disposed.</summary>
        internal static NotificationCenter Current { get; private set; }

        /// <summary>The handlers attached to one device, kept so exactly those can be removed again.</summary>
        private sealed class Subscription
        {
            internal UPS_Device.Lost_ConnectEventHandler LostConnect;
            internal UPS_Device.DisconnectedEventHandler Disconnected;
            internal UPS_Device.DataUpdatedEventHandler DataUpdated;
        }

        private readonly object gate = new object();
        private readonly Logger logger;
        private readonly CultureInfo uiCulture;
        private readonly CultureInfo formatCulture;
        private readonly ToastChannel toasts;
        private readonly EmailOutbox outbox;
        private readonly Dictionary<UPS_Device, Subscription> devices = new Dictionary<UPS_Device, Subscription>();

        // Everything below is guarded by gate.

        /// <summary>The device the shell attached last: the one shutdown and update events describe.</summary>
        private UPS_Device currentDevice;

        /// <summary>Which server and UPS the state below describes.</summary>
        private string deviceIdentity;

        /// <summary>When the connection broke, while it is broken.</summary>
        private DateTime? outageStartUtc;

        /// <summary>
        /// The statuses as this center last evaluated them, None when nothing is known about this UPS yet. Kept
        /// here rather than trusted to StatusesChanged: the device's own "previous status" is not reset by a
        /// reconnection and only moves when a flag appears, so a flag that clears and comes back (OB+LB, OB,
        /// OB+LB) is never reported, and a reconnection with an unchanged status reports nothing at all.
        /// </summary>
        private UPS_States knownStatus = UPS_States.None;

        /// <summary>
        /// How long the connection was down, between the reconnection and the first poll that follows it. The
        /// "restored" notification waits for that poll: sent straight from Connected it could only say "unknown"
        /// for every reading, because the device has not read anything yet.
        /// </summary>
        private TimeSpan? restoredAfter;

        /// <summary>The UPS was last seen on battery. Survives reconnections, so "power restored" can follow them.</summary>
        private bool onBattery;

        private volatile bool isDisposed;

        /// <param name="logger">The application logger.</param>
        /// <param name="showMainWindow">Brings the main window forward. Called on the UI thread.</param>
        /// <param name="balloonFallback">
        /// Shows a tray balloon (title, text, isCritical) where Windows toast notifications are unavailable.
        /// Called on the UI thread.
        /// </param>
        internal NotificationCenter(Logger logger, Action showMainWindow, Action<string, string, bool> balloonFallback)
        {
            this.logger = logger ?? AppGlobals.LogFile;

            // Captured here because the shell builds the center on the UI thread. Toast clicks arrive on a
            // background thread and must find their way back to it, and emails finished on the outbox worker must
            // be written in the language the user sees rather than whatever that thread defaults to.
            var uiContext = SynchronizationContext.Current;
            uiCulture = CultureInfo.CurrentUICulture;
            formatCulture = CultureInfo.CurrentCulture;

            toasts = new ToastChannel(this.logger, uiContext, uiCulture, showMainWindow, balloonFallback);
            outbox = new EmailOutbox(this.logger, uiCulture, formatCulture,
                                     Path.Combine(AppGlobals.DataDirectory, "Notifications", "outbox.json"));

            lock (currentSync)
            {
                Current = this;
            }
        }

        #region Devices

        /// <summary>Starts following a device's connection and status events. Safe to call with null.</summary>
        internal void Attach(UPS_Device device)
        {
            if (device == null)
            {
                return;
            }

            try
            {
                lock (gate)
                {
                    if (isDisposed || devices.ContainsKey(device))
                    {
                        return;
                    }

                    var identity = IdentityOf(device);

                    if (deviceIdentity != null && !string.Equals(deviceIdentity, identity, StringComparison.Ordinal))
                    {
                        // Another server or another UPS: an outage or a battery state of the old one says nothing
                        // about this one, and carrying it over would announce a restoration that never happened.
                        outageStartUtc = null;
                        onBattery = false;
                        knownStatus = UPS_States.None;
                    }

                    deviceIdentity = identity;
                    currentDevice = device;

                    // Lost_Connect, Disconnected and DataUpdated do not say which device raised them, hence the closures.
                    var subscription = new Subscription
                    {
                        LostConnect = () => OnLostConnect(device),
                        Disconnected = () => OnUserDisconnected(device),
                        DataUpdated = () => OnDataUpdated(device),
                    };

                    devices[device] = subscription;

                    device.Connected += OnConnected;
                    device.Lost_Connect += subscription.LostConnect;
                    device.Disconnected += subscription.Disconnected;
                    device.ReconnectAbandoned += OnReconnectAbandoned;
                    device.DataUpdated += subscription.DataUpdated;
                }
            }
            catch (Exception ex)
            {
                logger.LogTracing("Unable to follow the UPS device for notifications.", LogLvl.LOG_ERROR, this);
                logger.LogException(ex, this);
            }
        }

        /// <summary>Stops following a device. Safe to call with null or with a device that was never attached.</summary>
        internal void Detach(UPS_Device device)
        {
            if (device == null)
            {
                return;
            }

            try
            {
                Subscription subscription;

                lock (gate)
                {
                    if (!devices.TryGetValue(device, out subscription))
                    {
                        return;
                    }

                    devices.Remove(device);

                    if (currentDevice == device)
                    {
                        currentDevice = null;
                    }
                }

                Unsubscribe(device, subscription);
            }
            catch (Exception ex)
            {
                logger.LogException(ex, this);
            }
        }

        private void Unsubscribe(UPS_Device device, Subscription subscription)
        {
            device.Connected -= OnConnected;
            device.Lost_Connect -= subscription.LostConnect;
            device.Disconnected -= subscription.Disconnected;
            device.ReconnectAbandoned -= OnReconnectAbandoned;
            device.DataUpdated -= subscription.DataUpdated;
        }

        private static string IdentityOf(UPS_Device device)
        {
            var config = device.Nut_Config;

            return config == null
                ? string.Empty
                : (config.Host ?? string.Empty).Trim().ToLowerInvariant() + ":" + config.Port + "/" + config.UPSName;
        }

        /// <summary>
        /// An event can still be in flight on another thread when its device is detached; one from a device that is
        /// no longer followed describes a connection the shell has already thrown away. Caller holds gate.
        /// </summary>
        private bool IsFollowed(UPS_Device device)
        {
            return !isDisposed && device != null && devices.ContainsKey(device);
        }

        #endregion

        #region Device events

        private void OnLostConnect(UPS_Device device)
        {
            try
            {
                lock (gate)
                {
                    // UPS_Device reports a broken socket once and then retries quietly, but a reconnection that
                    // succeeds and breaks again on its first poll reports it anew. One outage, one notification.
                    // A disconnection the user asked for never gets here: the device does not treat it as lost.
                    if (!IsFollowed(device) || outageStartUtc.HasValue)
                    {
                        return;
                    }

                    outageStartUtc = DateTime.UtcNow;

                    // Reconnected and lost again before a single poll went through: that was not a restoration.
                    restoredAfter = null;
                }

                // The device decides whether to retry before it reports the loss. When it will not - automatic
                // reconnection is off - it has already said so through ReconnectAbandoned, and a ConnectionLost
                // promising that NutDesk "will keep trying" would replace that toast with the opposite of the truth.
                // The outage is still recorded, so a manual reconnection is announced as a restoration.
                if (!device.IsReconnecting)
                {
                    return;
                }

                var snapshot = UpsSnapshot.Capture(device);
                Publish(NotificationKind.ConnectionLost, snapshot, snapshot.ServerText(Unknown));
            }
            catch (Exception ex)
            {
                LogHandlerFailure("Lost_Connect", ex);
            }
        }

        private void OnConnected(UPS_Device sender)
        {
            try
            {
                lock (gate)
                {
                    if (!IsFollowed(sender))
                    {
                        return;
                    }

                    // Nothing to reset for the status: the first poll after this is compared with knownStatus, which
                    // still describes the UPS as it was before the break - so only what changed meanwhile is news.

                    // Only a connection that was lost is "restored"; the first one of a session is not news. It is
                    // announced by the first poll after this (OnDataUpdated), with real readings in it.
                    if (outageStartUtc.HasValue)
                    {
                        restoredAfter = DateTime.UtcNow - outageStartUtc.Value;
                        outageStartUtc = null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHandlerFailure("Connected", ex);
            }
        }

        /// <summary>
        /// The user asked to disconnect. Whatever outage was in progress is over as far as they are concerned: the
        /// next connection is one they make on purpose, not a restoration.
        /// </summary>
        private void OnUserDisconnected(UPS_Device device)
        {
            try
            {
                lock (gate)
                {
                    if (!IsFollowed(device))
                    {
                        return;
                    }

                    outageStartUtc = null;
                    restoredAfter = null;
                    knownStatus = UPS_States.None;
                }
            }
            catch (Exception ex)
            {
                LogHandlerFailure("Disconnected", ex);
            }
        }

        private void OnReconnectAbandoned(UPS_Device sender, string reason)
        {
            try
            {
                // The device also reports "abandoned" when the user merely turns automatic reconnection off while
                // connected. Nothing has stopped then, and saying "monitoring stopped" would be false.
                if (sender == null || sender.IsConnected)
                {
                    return;
                }

                lock (gate)
                {
                    if (!IsFollowed(sender))
                    {
                        return;
                    }
                }

                // The device explains itself in English, for the log. The commonest case - the user turned automatic
                // reconnection off - gets a translated sentence; the rarer configuration problems (unknown UPS,
                // access denied) keep the technical text, which is what someone fixing them needs anyway.
                var explanation = sender.Nut_Config != null && !sender.Nut_Config.AutoReconnect
                    ? NotificationText.Get("Notif_Reason_AutoReconnectOff", uiCulture)
                    : reason ?? string.Empty;

                var snapshot = UpsSnapshot.Capture(sender);
                Publish(NotificationKind.ReconnectAbandoned, snapshot, snapshot.ServerText(Unknown), explanation);
            }
            catch (Exception ex)
            {
                LogHandlerFailure("ReconnectAbandoned", ex);
            }
        }

        /// <summary>
        /// Runs after every successful poll and compares the UPS with what this center last saw.
        /// </summary>
        /// <remarks>
        /// The first evaluation after a connection is a baseline. When nothing is known about this UPS - the first
        /// connection of the session, or the first after the user disconnected on purpose - every abnormal flag
        /// counts as news, because a UPS already on battery when NutDesk connects is exactly what the user needs to
        /// hear. After an automatic reconnection the previous state is still known, so only what changed while
        /// NutDesk could not see the UPS is reported: repeating "battery needs replacing" after every network
        /// hiccup would teach the user to ignore the notifications.
        /// </remarks>
        private void OnDataUpdated(UPS_Device sender)
        {
            try
            {
                var current = sender?.UPS_Datas?.UPS_Value?.UPS_Status ?? UPS_States.None;
                if (current == UPS_States.None)
                {
                    // No status read yet: nothing to compare, and no reason to consume the baseline.
                    return;
                }

                var raised = new List<NotificationKind>();
                TimeSpan? restored = null;

                lock (gate)
                {
                    if (!IsFollowed(sender))
                    {
                        return;
                    }

                    restored = restoredAfter;
                    restoredAfter = null;

                    // With nothing known every active flag is rising; otherwise only the ones that were not active.
                    var rising = current & ~knownStatus;
                    knownStatus = current;

                    if (Has(rising, UPS_States.OB))
                    {
                        raised.Add(NotificationKind.PowerLost);
                    }
                    else if (Has(rising, UPS_States.OL) && !Has(current, UPS_States.OB) && onBattery)
                    {
                        // Only after the UPS was seen on battery: plain OL is simply how a healthy UPS starts. That
                        // includes a baseline after a reconnection, when the battery state was learnt before the
                        // break and the power came back while we could not see it.
                        raised.Add(NotificationKind.PowerRestored);
                    }

                    if (Has(rising, UPS_States.LB))
                    {
                        raised.Add(NotificationKind.LowBattery);
                    }

                    if (Has(rising, UPS_States.RB))
                    {
                        raised.Add(NotificationKind.BatteryReplace);
                    }

                    if (Has(rising, UPS_States.OVER))
                    {
                        raised.Add(NotificationKind.Overload);
                    }

                    // FSD is deliberately absent: the shell decides what an FSD means and reports the shutdown
                    // itself through NotifyShutdownStarted.

                    if (Has(current, UPS_States.OB))
                    {
                        onBattery = true;
                    }
                    else if (Has(current, UPS_States.OL))
                    {
                        onBattery = false;
                    }
                }

                if (!restored.HasValue && raised.Count == 0)
                {
                    return;
                }

                var snapshot = UpsSnapshot.Capture(sender);

                if (restored.HasValue)
                {
                    // Before any status news, so the reader learns the connection is back before what it found.
                    Publish(NotificationKind.ConnectionRestored, snapshot, snapshot.ServerText(Unknown),
                            UpsSnapshot.FormatDuration(restored.Value, formatCulture));
                }

                foreach (var kind in raised)
                {
                    Publish(kind, snapshot, StatusBodyArguments(kind, snapshot));
                }
            }
            catch (Exception ex)
            {
                LogHandlerFailure("DataUpdated", ex);
            }
        }

        private static bool Has(UPS_States statuses, UPS_States flag)
        {
            return (statuses & flag) == flag;
        }

        private object[] StatusBodyArguments(NotificationKind kind, UpsSnapshot snapshot)
        {
            var unknown = Unknown;
            var ups = snapshot.UpsNameText(unknown);

            if (kind == NotificationKind.Overload)
            {
                return new object[] { ups, snapshot.LoadNumber(formatCulture, unknown) };
            }

            // PowerLost and LowBattery use all three, PowerRestored the first two, BatteryReplace the first;
            // string.Format ignores the ones a template does not mention.
            return new object[]
            {
                ups,
                snapshot.ChargeNumber(formatCulture, unknown),
                snapshot.RuntimeText(formatCulture, unknown),
            };
        }

        private void LogHandlerFailure(string eventName, Exception ex)
        {
            logger.LogTracing("Unable to process the " + eventName + " event for notifications.", LogLvl.LOG_ERROR, this);
            logger.LogException(ex, this);
        }

        #endregion

        #region Shell events

        /// <param name="forcedByServer">True for an FSD from the NUT server, false for the battery limits.</param>
        internal void NotifyShutdownStarted(bool forcedByServer)
        {
            try
            {
                var reason = NotificationText.Get(forcedByServer ? "Notif_Reason_Fsd" : "Notif_Reason_BatteryLimit",
                                                  uiCulture);
                Publish(NotificationKind.ShutdownStarted, CaptureCurrentDevice(), reason);
            }
            catch (Exception ex)
            {
                LogHandlerFailure("shutdown started", ex);
            }
        }

        internal void NotifyShutdownCancelled()
        {
            try
            {
                Publish(NotificationKind.ShutdownCancelled, CaptureCurrentDevice());
            }
            catch (Exception ex)
            {
                LogHandlerFailure("shutdown cancelled", ex);
            }
        }

        /// <param name="version">The version available, as the user reads it ("1.1.0").</param>
        internal void NotifyUpdateAvailable(string version)
        {
            try
            {
                Publish(NotificationKind.UpdateAvailable, CaptureCurrentDevice(),
                        string.IsNullOrWhiteSpace(version) ? Unknown : version.Trim());
            }
            catch (Exception ex)
            {
                LogHandlerFailure("update available", ex);
            }
        }

        /// <summary>
        /// The first start after an in-place update. A toast only, and only when Windows notifications are on: it
        /// is news for whoever sits at this screen, and the event log already has the line. It is not a kind of
        /// its own, since the kinds are what each channel can be subscribed to; it goes out under the topic of
        /// <see cref="NotificationKind.UpdateAvailable"/>, so it replaces the toast that announced the update.
        /// </summary>
        internal void NotifyUpdated(string fromVersion, string toVersion)
        {
            try
            {
                if (isDisposed || !Properties.Settings.Default.NT_ToastEnabled)
                {
                    return;
                }

                var title = NotificationText.Get("Notif_Updated_Title", uiCulture);
                var body = NotificationText.Format(formatCulture, NotificationText.Get("Update_UpdatedFrom", uiCulture),
                                                   fromVersion, toVersion);
                toasts.Show(new NotificationEvent(NotificationKind.UpdateAvailable, CaptureCurrentDevice(), title, body), false);
            }
            catch (Exception ex)
            {
                LogHandlerFailure("update completed", ex);
            }
        }

        private UpsSnapshot CaptureCurrentDevice()
        {
            UPS_Device device;

            lock (gate)
            {
                device = currentDevice;
            }

            return UpsSnapshot.Capture(device);
        }

        #endregion

        #region Routing

        private string Unknown => NotificationText.Get("Notif_Unknown", uiCulture);

        /// <summary>The log level a status event is shown with, or null for the events the shell logs itself.</summary>
        private static LogLvl? DisplayLevelOf(NotificationKind kind)
        {
            switch (kind)
            {
                case NotificationKind.PowerRestored:
                    return LogLvl.LOG_NOTICE;
                case NotificationKind.PowerLost:
                case NotificationKind.BatteryReplace:
                    return LogLvl.LOG_WARNING;
                case NotificationKind.LowBattery:
                case NotificationKind.Overload:
                    return LogLvl.LOG_ERROR;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Renders the event once and hands it to every channel the user wants for this kind. The settings are read
        /// on every call: the preferences can change at any moment and apply from the next event on.
        /// </summary>
        private void Publish(NotificationKind kind, UpsSnapshot snapshot, params object[] bodyArguments)
        {
            if (isDisposed)
            {
                return;
            }

            var title = NotificationText.Title(kind, uiCulture);
            var body = NotificationText.Format(formatCulture, NotificationText.Body(kind, uiCulture), bodyArguments);
            var notification = new NotificationEvent(kind, snapshot, title, body);

            // The UPS status events are exactly what the event log's "important events" view is for - a power cut
            // at a glance - so they carry their translated wording as the display text, at a level that colours
            // them by severity. The connection, shutdown and update events are already logged that way by the
            // shell, and a second line for each would only duplicate them.
            var displayLevel = DisplayLevelOf(kind);
            if (displayLevel.HasValue)
            {
                logger.LogTracing("Notification " + kind + ": " + body, displayLevel.Value, this, title + ": " + body);
            }
            else
            {
                logger.LogTracing("Notification " + kind + ": " + body, LogLvl.LOG_NOTICE, this);
            }

            bool toastEnabled = false, persistCritical = false, emailEnabled = false;
            int toastEvents = 0, emailEvents = 0;

            try
            {
                var settings = Properties.Settings.Default;
                toastEnabled = settings.NT_ToastEnabled;
                persistCritical = settings.NT_ToastPersistCritical;
                toastEvents = settings.NT_ToastEvents;
                emailEnabled = settings.NT_EmailEnabled;
                emailEvents = settings.NT_EmailEvents;
            }
            catch (Exception ex)
            {
                logger.LogTracing("Unable to read the notification settings.", LogLvl.LOG_ERROR, this);
                logger.LogException(ex, this);
            }

            if (toastEnabled && (toastEvents & (int)kind) != 0)
            {
                toasts.Show(notification, persistCritical);
            }

            if (emailEnabled && (emailEvents & (int)kind) != 0)
            {
                QueueEmail(notification);
            }
        }

        private void QueueEmail(NotificationEvent notification)
        {
            EmailAccount account = null;

            try
            {
                account = EmailAccount.FromSettings();
            }
            catch (Exception ex)
            {
                logger.LogTracing("Unable to read the email account from the settings.", LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
            }

            if (account == null || !account.IsComplete)
            {
                logger.LogTracing("Email notification for " + notification.Kind + " skipped: the email account " +
                                  "is not completely configured.", LogLvl.LOG_WARNING, this);
                return;
            }

            outbox.Offer(ComposeDraft(notification, account.SubjectPrefix));
        }

        /// <summary>
        /// Writes the email now, while the snapshot and the user's language are those of the event. The outbox only
        /// adds notes about its own delays.
        /// </summary>
        private EmailDraft ComposeDraft(NotificationEvent notification, string subjectPrefix)
        {
            var snapshot = notification.Snapshot;
            var unknown = Unknown;

            var subject = notification.Title;

            if (snapshot.UpsName != null)
            {
                subject += " - " + snapshot.UpsName;
            }

            subject = ComposeSubject(subjectPrefix, subject);

            var details = new StringBuilder();
            details.AppendLine(NotificationText.Get("Email_Section_Details", uiCulture));
            AppendField(details, "Email_Field_Time",
                        snapshot.TimestampUtc.ToLocalTime().ToString("G", formatCulture));
            AppendField(details, "Email_Field_Computer", snapshot.Computer ?? unknown);
            AppendField(details, "Email_Field_Server", snapshot.ServerText(unknown));
            AppendField(details, "Email_Field_Ups", snapshot.UpsNameText(unknown));
            AppendField(details, "Email_Field_Status", snapshot.StatusText(unknown));
            AppendField(details, "Email_Field_Charge",
                        snapshot.ChargePercent.HasValue ? snapshot.ChargeNumber(formatCulture, unknown) + "%" : unknown);
            AppendField(details, "Email_Field_Runtime", snapshot.RuntimeText(formatCulture, unknown));
            AppendField(details, "Email_Field_Load",
                        snapshot.LoadPercent.HasValue ? snapshot.LoadNumber(formatCulture, unknown) + "%" : unknown);
            AppendField(details, "Email_Field_InputVoltage", snapshot.InputVoltageText(formatCulture, unknown));
            details.AppendLine();
            details.Append(NotificationText.Format(formatCulture, NotificationText.Get("Email_Footer", uiCulture),
                                                   snapshot.Computer ?? unknown));

            return new EmailDraft(notification.Kind, notification.EventUtc, subject, notification.Body,
                                  details.ToString());
        }

        private void AppendField(StringBuilder builder, string labelKey, string value)
        {
            builder.Append(NotificationText.Get(labelKey, uiCulture)).Append(": ").AppendLine(value);
        }

        /// <summary>"[NutDesk] Power failure - myups", without a leading space when the prefix is empty.</summary>
        private static string ComposeSubject(string prefix, string text)
        {
            prefix = (prefix ?? string.Empty).Trim();
            return prefix.Length == 0 ? text : prefix + " " + text;
        }

        #endregion

        #region Email

        /// <summary>
        /// Gives queued emails up to <paramref name="timeout"/> to go out; returns true when the outbox is empty.
        /// Called just before the computer is shut down.
        /// </summary>
        internal bool FlushEmail(TimeSpan timeout)
        {
            try
            {
                return outbox.Flush(timeout);
            }
            catch (Exception ex)
            {
                logger.LogException(ex, this);
                return false;
            }
        }

        /// <summary>Emails queued and not yet delivered, including those waiting from a previous session.</summary>
        internal int PendingEmailCount
        {
            get
            {
                try
                {
                    return outbox.Count;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Sends a test message with <paramref name="account"/> as it is, bypassing the outbox and every filter.
        /// Completes with null on success, otherwise with a short error message fit to show the user.
        /// </summary>
        internal static Task<string> SendTestEmailAsync(EmailAccount account)
        {
            var logger = Current?.logger ?? AppGlobals.LogFile;

            try
            {
                // Read on the caller's thread, which is the preferences window's: the language the user is looking at.
                var culture = CultureInfo.CurrentUICulture;

                if (account == null || !account.IsComplete)
                {
                    return Task.FromResult(EmailSender.DescribeIncomplete(account, culture));
                }

                var email = new OutgoingEmail(ComposeSubject(account.SubjectPrefix,
                                                             NotificationText.Get("Email_TestSubject", culture)),
                                              NotificationText.Get("Email_TestBody", culture),
                                              DateTimeOffset.Now);
                var server = UpsSnapshot.FormatServer(account.Host, account.Port);

                logger.LogTracing("Sending a test email through " + server + ".", LogLvl.LOG_NOTICE, null);

                var send = Task.Run(() =>
                {
                    Exception failure = null;

                    using (var cancel = new CancellationTokenSource(TEST_EMAIL_TIMEOUT))
                    {
                        try
                        {
                            EmailSender.SendBatch(account, new[] { email }, cancel.Token, (index, error) => failure = error);
                        }
                        catch (Exception ex)
                        {
                            failure = ex;
                        }
                    }

                    return failure;
                });

                return CompleteTestAsync(send, server, logger);
            }
            catch (Exception ex)
            {
                logger.LogException(ex, null);
                return Task.FromResult(EmailSender.Describe(ex));
            }
        }

        private static async Task<string> CompleteTestAsync(Task<Exception> send, string server, Logger logger)
        {
            try
            {
                // The cancellation above covers MailKit; this covers whatever does not honour it (a DNS lookup
                // inside the framework), so the preferences window gets its answer on time regardless.
                var finished = await Task.WhenAny(send, Task.Delay(TEST_EMAIL_TIMEOUT + TimeSpan.FromSeconds(2)))
                                         .ConfigureAwait(false);

                if (finished != send)
                {
                    _ = send.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    logger.LogTracing("Test email through " + server + " timed out.", LogLvl.LOG_WARNING, null);
                    return new TimeoutException().Message;
                }

                var failure = await send.ConfigureAwait(false);

                if (failure == null)
                {
                    logger.LogTracing("Test email sent through " + server + ".", LogLvl.LOG_NOTICE, null);
                    return null;
                }

                var message = EmailSender.Describe(failure);
                logger.LogTracing("Test email through " + server + " failed (" + failure.GetType().Name + "): " +
                                  message, LogLvl.LOG_WARNING, null);
                return message;
            }
            catch (Exception ex)
            {
                logger.LogException(ex, null);
                return EmailSender.Describe(ex);
            }
        }

        #endregion

        public void Dispose()
        {
            List<KeyValuePair<UPS_Device, Subscription>> followed;

            lock (gate)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                followed = devices.ToList();
                devices.Clear();
                currentDevice = null;
            }

            lock (currentSync)
            {
                if (Current == this)
                {
                    Current = null;
                }
            }

            foreach (var entry in followed)
            {
                try
                {
                    Unsubscribe(entry.Key, entry.Value);
                }
                catch (Exception ex)
                {
                    logger.LogException(ex, this);
                }
            }

            try
            {
                toasts.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogException(ex, this);
            }

            try
            {
                outbox.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogException(ex, this);
            }
        }
    }
}
