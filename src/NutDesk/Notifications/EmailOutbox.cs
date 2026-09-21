using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NutDesk.Common;

namespace NutDesk.Notifications
{
    /// <summary>One email waiting in the outbox, exactly as it is persisted.</summary>
    /// <remarks>
    /// The account is deliberately absent: it is read from the settings at send time, so correcting a wrong
    /// password or server releases everything that was stuck behind it - and no password ever reaches this file.
    /// </remarks>
    internal sealed class OutboxItem
    {
        public Guid Id { get; set; }

        public NotificationKind Kind { get; set; }

        /// <summary>When the email was written into the outbox.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>When the event happened; becomes the Date header.</summary>
        public DateTime EventUtc { get; set; }

        public string Subject { get; set; }

        /// <summary>Rendered when the event happened, in the language the user had then.</summary>
        public string Body { get; set; }

        public int Attempts { get; set; }

        public DateTime NextAttemptUtc { get; set; }

        /// <summary>Why the last attempt failed. For the log file and for anyone opening outbox.json.</summary>
        public string LastError { get; set; }
    }

    /// <summary>
    /// Keeps every email until it has been delivered, across network outages and application restarts.
    /// </summary>
    /// <remarks>
    /// During a power cut the router is often down too, so the email written at the moment of the event usually
    /// cannot go out right then. It is written to disk and a single background worker keeps offering it to the
    /// server - with a growing pause between attempts - until the network comes back, the message is 48 hours old,
    /// or the application closes, in which case the next session picks it up.
    ///
    /// All state is guarded by <see cref="sync"/>. The worker never needs the UI thread, which is what lets the UI
    /// thread block in <see cref="Flush"/> without deadlocking.
    /// </remarks>
    internal sealed class EmailOutbox : IDisposable
    {
        private static readonly TimeSpan MAX_AGE = TimeSpan.FromHours(48);

        /// <summary>Beyond this the recipient deserves to know the message was held up, and roughly why.</summary>
        private static readonly TimeSpan DELAYED_THRESHOLD = TimeSpan.FromMinutes(2);

        /// <summary>
        /// The longest the worker sleeps without looking again, so a cooldown changed in the preferences or an item
        /// reaching its 48 hours is noticed without anyone having to wake it.
        /// </summary>
        private static readonly TimeSpan MAX_IDLE_WAIT = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Pause after the first, second, ... failure. Quick at first, because a router rebooting after a power
        /// cut is back within a minute or two; then every quarter of an hour, which costs nothing over a long outage.
        /// </summary>
        private static readonly TimeSpan[] BACKOFF =
        {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(10),
        };

        private static readonly TimeSpan BACKOFF_CEILING = TimeSpan.FromMinutes(15);

        /// <summary>How often to look again while the account is incomplete, should no settings change wake us first.</summary>
        private static readonly TimeSpan INCOMPLETE_ACCOUNT_RETRY = TimeSpan.FromMinutes(1);

        /// <summary>How long Dispose waits for the worker after cancelling it. Cancellation normally lands at once.</summary>
        private static readonly TimeSpan WORKER_JOIN_TIMEOUT = TimeSpan.FromSeconds(3);

        private static readonly JsonSerializerSettings JSON_SETTINGS = new JsonSerializerSettings
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            Formatting = Formatting.Indented,
        };

        private readonly object sync = new object();

        /// <summary>Serialises writers of the file; always taken before <see cref="sync"/>, never while holding it.</summary>
        private readonly object fileSync = new object();

        private readonly List<OutboxItem> items;
        private readonly EmailThrottle throttle = new EmailThrottle();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly Thread worker;
        private readonly Logger logger;
        private readonly CultureInfo uiCulture;
        private readonly CultureInfo formatCulture;
        private readonly string path;

        private bool dirty;
        private long stateVersion;
        private long writtenVersion;
        private int flushRequests;
        private bool isDisposed;

        internal EmailOutbox(Logger logger, CultureInfo uiCulture, CultureInfo formatCulture, string path)
        {
            this.logger = logger;
            this.uiCulture = uiCulture;
            this.formatCulture = formatCulture;
            this.path = path;

            items = Load();

            Properties.Settings.Default.PropertyChanged += OnSettingChanged;

            worker = new Thread(Run)
            {
                IsBackground = true,
                Name = "NutDesk email outbox",
                CurrentCulture = formatCulture,
                CurrentUICulture = uiCulture,
            };
            worker.Start();
        }

        private void OnSettingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == null || !e.PropertyName.StartsWith("NT_", StringComparison.Ordinal))
                {
                    return;
                }

                lock (sync)
                {
                    if (isDisposed)
                    {
                        return;
                    }

                    // The account may just have been fixed: whatever the queue was waiting out, try now.
                    var now = DateTime.UtcNow;
                    foreach (var item in items)
                    {
                        if (item.NextAttemptUtc > now)
                        {
                            item.NextAttemptUtc = now;
                        }
                    }
                }

                wake.Set();
            }
            catch (Exception ex)
            {
                logger.LogException(ex, this);
            }
        }

        /// <summary>Emails in the outbox, not counting events still held back by the cooldown.</summary>
        internal int Count
        {
            get
            {
                lock (sync)
                {
                    return items.Count;
                }
            }
        }

        /// <summary>Hands a new email to the outbox, through the cooldown. Returns at once.</summary>
        internal void Offer(EmailDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            lock (sync)
            {
                if (isDisposed)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var cooldown = ReadCooldown();

                if (throttle.Offer(draft, now, cooldown, out var release))
                {
                    Enqueue(release, now, cooldown);
                }
                else
                {
                    logger.LogTracing("Email for " + draft.Kind + " held back by the " + cooldown.TotalMinutes +
                                      " min cooldown; the latest state will be sent when it expires.",
                                      LogLvl.LOG_NOTICE, this);
                }
            }

            wake.Set();
        }

        /// <summary>
        /// Wakes the worker, releases every held-back event and waits until the outbox is empty or
        /// <paramref name="timeout"/> has passed. Safe on the UI thread: the worker never needs it.
        /// </summary>
        internal bool Flush(TimeSpan timeout)
        {
            var elapsed = Stopwatch.StartNew();

            if (!AccountIsUsable())
            {
                // Nothing can leave with an incomplete account, so waiting would only hold up the shutdown. The
                // held-back events still go into the outbox on disk, for the next session.
                lock (sync)
                {
                    try
                    {
                        ReleaseHeldBack(DateTime.UtcNow, ReadCooldown(), true);
                        MarkChanged();
                    }
                    catch (Exception ex)
                    {
                        logger.LogException(ex, this);
                    }
                }

                PersistIfDirty();

                lock (sync)
                {
                    return items.Count == 0;
                }
            }

            lock (sync)
            {
                if (isDisposed)
                {
                    return items.Count == 0;
                }

                flushRequests++;

                // Whatever back-off an item was serving, the computer is about to go down: now is its last chance.
                var now = DateTime.UtcNow;
                foreach (var item in items)
                {
                    if (item.NextAttemptUtc > now)
                    {
                        item.NextAttemptUtc = now;
                    }
                }
            }

            try
            {
                wake.Set();

                lock (sync)
                {
                    while (items.Count > 0 || throttle.HasPending)
                    {
                        var remaining = timeout - elapsed.Elapsed;

                        if (remaining <= TimeSpan.Zero || isDisposed)
                        {
                            return false;
                        }

                        // Sliced, so a change that forgot to pulse can only delay the answer, never lose it.
                        Monitor.Wait(sync, remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1));
                    }

                    return true;
                }
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            finally
            {
                lock (sync)
                {
                    flushRequests--;
                }
            }
        }

        private bool AccountIsUsable()
        {
            try
            {
                return EmailAccount.FromSettings().IsComplete;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #region Worker

        private void Run()
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    List<OutboxItem> due;
                    var wait = MAX_IDLE_WAIT;

                    lock (sync)
                    {
                        var now = DateTime.UtcNow;
                        var cooldown = ReadCooldown();

                        ReleaseHeldBack(now, cooldown, flushRequests > 0);
                        DropExpired(now);

                        due = items.Where(i => i.NextAttemptUtc <= now).OrderBy(i => i.CreatedUtc).ToList();

                        if (due.Count == 0)
                        {
                            wait = TimeUntilNextWork(now, cooldown);
                        }

                        Monitor.PulseAll(sync);
                    }

                    PersistIfDirty();

                    if (due.Count == 0)
                    {
                        WaitHandle.WaitAny(new[] { wake, stop.Token.WaitHandle }, wait);
                        continue;
                    }

                    Deliver(due);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // The worker is the only way anything leaves the outbox; it must survive whatever happened.
                    logger.LogTracing("Unexpected error in the email outbox worker; resuming shortly.",
                                      LogLvl.LOG_ERROR, this);
                    logger.LogException(ex, this);

                    try
                    {
                        stop.Token.WaitHandle.WaitOne(BACKOFF[0]);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            }
        }

        private void Deliver(List<OutboxItem> due)
        {
            EmailAccount account = null;

            try
            {
                account = EmailAccount.FromSettings();
            }
            catch (Exception ex)
            {
                // Typically a password DPAPI cannot decrypt any more (profile restored on another machine).
                logger.LogTracing("Unable to read the email account from the settings.", LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
            }

            if (account == null || !account.IsComplete)
            {
                // Kept, not discarded: completing the account in the preferences releases the queue. Nothing was
                // attempted, so it is not counted as a failure - counting it would push the items up the back-off
                // ladder and leave them waiting a quarter of an hour after the account had been fixed. A saved
                // setting wakes the worker at once anyway (see OnSettingChanged); this is only the fallback.
                lock (sync)
                {
                    var retry = DateTime.UtcNow + INCOMPLETE_ACCOUNT_RETRY;
                    foreach (var item in due)
                    {
                        item.LastError = "The email account in the settings is incomplete.";
                        item.NextAttemptUtc = retry;
                    }

                    MarkChanged();
                }

                logger.LogTracing(due.Count + " email notification(s) waiting: the email account in the settings is incomplete.",
                                  LogLvl.LOG_NOTICE, this);
                return;
            }

            var server = UpsSnapshot.FormatServer(account.Host, account.Port);
            var now = DateTime.UtcNow;
            var messages = due.Select(item => new OutgoingEmail(item.Subject, WithDelayNote(item, now),
                                                                new DateTimeOffset(item.EventUtc).ToLocalTime()))
                              .ToList();

            logger.LogTracing("Sending " + messages.Count + " email notification(s) through " + server + ".",
                              LogLvl.LOG_NOTICE, this);

            try
            {
                EmailSender.SendBatch(account, messages, stop.Token, (index, error) => Complete(due[index], error, server));
            }
            catch (OperationCanceledException)
            {
                logger.LogTracing("Email delivery interrupted because NutDesk is closing; the outbox is kept for next time.",
                                  LogLvl.LOG_NOTICE, this);
            }
        }

        /// <summary>Records the outcome of one attempt.</summary>
        private void Complete(OutboxItem item, Exception error, string server)
        {
            lock (sync)
            {
                if (!items.Contains(item))
                {
                    return;
                }

                if (error == null)
                {
                    items.Remove(item);
                    logger.LogTracing("Email notification " + item.Kind + " sent through " + server +
                                      " (attempt " + (item.Attempts + 1) + ").", LogLvl.LOG_NOTICE, this);
                }
                else
                {
                    item.Attempts++;
                    item.LastError = EmailSender.Describe(error);
                    item.NextAttemptUtc = DateTime.UtcNow + BackoffAfter(item.Attempts);

                    logger.LogTracing("Email notification " + item.Kind + " not sent" +
                                      (server != null ? " through " + server : string.Empty) +
                                      " (attempt " + item.Attempts + ", " + error.GetType().Name + "): " +
                                      item.LastError + " Next attempt at " +
                                      item.NextAttemptUtc.ToLocalTime().ToString("T", CultureInfo.InvariantCulture) + ".",
                                      LogLvl.LOG_WARNING, this);
                }

                MarkChanged();
            }
        }

        private string WithDelayNote(OutboxItem item, DateTime nowUtc)
        {
            if (nowUtc - item.CreatedUtc <= DELAYED_THRESHOLD)
            {
                return item.Body;
            }

            var note = NotificationText.Format(formatCulture, NotificationText.Get("Email_DelayedNote", uiCulture),
                                               item.CreatedUtc.ToLocalTime().ToString("g", formatCulture),
                                               nowUtc.ToLocalTime().ToString("g", formatCulture));

            return note + Environment.NewLine + Environment.NewLine + item.Body;
        }

        private static TimeSpan BackoffAfter(int failures)
        {
            return failures >= 1 && failures <= BACKOFF.Length ? BACKOFF[failures - 1] : BACKOFF_CEILING;
        }

        #endregion

        #region State (callers hold sync)

        private void ReleaseHeldBack(DateTime nowUtc, TimeSpan cooldown, bool releaseAll)
        {
            var released = throttle.TakeDue(nowUtc, cooldown, releaseAll, stale =>
                logger.LogTracing("Held-back email for " + stale.Kind + " dropped: its opposite has happened since, " +
                                  "so it no longer describes the present.", LogLvl.LOG_NOTICE, this));

            foreach (var release in released)
            {
                Enqueue(release, nowUtc, cooldown);
            }
        }

        private void Enqueue(EmailThrottle.Release release, DateTime nowUtc, TimeSpan cooldown)
        {
            string note = null;

            if (release.UnsentCount > 0)
            {
                note = NotificationText.Format(formatCulture, NotificationText.Get("Email_SuppressedNote", uiCulture),
                                               release.UnsentCount, (int)Math.Round(cooldown.TotalMinutes));
            }

            var draft = release.Draft;

            items.Add(new OutboxItem
            {
                Id = Guid.NewGuid(),
                Kind = draft.Kind,
                CreatedUtc = nowUtc,
                EventUtc = draft.EventUtc,
                Subject = draft.Subject,
                Body = draft.ComposeBody(note),
                Attempts = 0,
                NextAttemptUtc = nowUtc,
            });

            MarkChanged();
        }

        private void DropExpired(DateTime nowUtc)
        {
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];

                if (nowUtc - item.CreatedUtc > MAX_AGE)
                {
                    items.RemoveAt(i);
                    logger.LogTracing("Email notification " + item.Kind + " written at " +
                                      item.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.InvariantCulture) +
                                      " dropped after 48 hours without delivery. Last error: " +
                                      (item.LastError ?? "none") + ".", LogLvl.LOG_WARNING, this);
                    MarkChanged();
                }
            }
        }

        private TimeSpan TimeUntilNextWork(DateTime nowUtc, TimeSpan cooldown)
        {
            DateTime? next = throttle.NextDueUtc(cooldown);

            foreach (var item in items)
            {
                if (!next.HasValue || item.NextAttemptUtc < next.Value)
                {
                    next = item.NextAttemptUtc;
                }
            }

            if (!next.HasValue)
            {
                return MAX_IDLE_WAIT;
            }

            var wait = next.Value - nowUtc;

            if (wait < TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            return wait > MAX_IDLE_WAIT ? MAX_IDLE_WAIT : wait;
        }

        private void MarkChanged()
        {
            dirty = true;
            stateVersion++;
            Monitor.PulseAll(sync);
        }

        /// <summary>Read live: the user may change it at any time. 0 turns the cooldown off.</summary>
        private TimeSpan ReadCooldown()
        {
            try
            {
                var minutes = Properties.Settings.Default.NT_EmailCooldownMin;
                return TimeSpan.FromMinutes(Math.Max(0, Math.Min(minutes, 24 * 60)));
            }
            catch (Exception)
            {
                return TimeSpan.Zero;
            }
        }

        #endregion

        #region Persistence

        private List<OutboxItem> Load()
        {
            var loaded = new List<OutboxItem>();

            try
            {
                if (!File.Exists(path))
                {
                    return loaded;
                }

                var json = File.ReadAllText(path, Encoding.UTF8);
                loaded = JsonConvert.DeserializeObject<List<OutboxItem>>(json, JSON_SETTINGS) ?? new List<OutboxItem>();
            }
            catch (Exception ex)
            {
                // An unreadable outbox must not stop monitoring. It is set aside rather than overwritten, so
                // whatever it held can still be recovered by hand.
                logger.LogTracing("The email outbox could not be read and has been set aside; starting empty.",
                                  LogLvl.LOG_WARNING, this);
                logger.LogException(ex, this);
                SetAsideCorruptFile();
                return new List<OutboxItem>();
            }

            var now = DateTime.UtcNow;
            var kept = new List<OutboxItem>();

            foreach (var item in loaded)
            {
                if (item == null)
                {
                    continue;
                }

                if (now - item.CreatedUtc > MAX_AGE)
                {
                    logger.LogTracing("Email notification " + item.Kind + " left from a previous session dropped: " +
                                      "older than 48 hours.", LogLvl.LOG_WARNING, this);
                    continue;
                }

                if (item.Id == Guid.Empty)
                {
                    item.Id = Guid.NewGuid();
                }

                item.Subject = item.Subject ?? string.Empty;
                item.Body = item.Body ?? string.Empty;

                // A new session is a new chance: whatever kept the network down last time may well be over.
                item.NextAttemptUtc = now;
                kept.Add(item);
            }

            if (kept.Count > 0)
            {
                logger.LogTracing(kept.Count + " email notification(s) left from the previous session will be sent.",
                                  LogLvl.LOG_NOTICE, this);
            }

            if (kept.Count != loaded.Count)
            {
                dirty = true;
                stateVersion++;
            }

            return kept;
        }

        private void PersistIfDirty()
        {
            string json;
            long version;

            lock (sync)
            {
                if (!dirty)
                {
                    return;
                }

                dirty = false;
                version = stateVersion;
                json = JsonConvert.SerializeObject(items, JSON_SETTINGS);
            }

            lock (fileSync)
            {
                // Dispose may persist while an abandoned worker is still finishing: never let the older state win.
                if (version <= writtenVersion)
                {
                    return;
                }

                try
                {
                    WriteAtomically(json);
                    writtenVersion = version;
                }
                catch (Exception ex)
                {
                    logger.LogTracing("Unable to save the email outbox; will try again.", LogLvl.LOG_WARNING, this);
                    logger.LogException(ex, this);

                    lock (sync)
                    {
                        dirty = true;
                    }
                }
            }
        }

        /// <summary>
        /// Temp file then replace: power can fail at any moment - that is the whole premise of this application -
        /// and a half-written outbox would lose every message in it.
        /// </summary>
        private void WriteAtomically(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var temp = path + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, null, true);
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is PlatformNotSupportedException ||
                                           ex is UnauthorizedAccessException)
                {
                    // Some file systems (network shares, FAT) cannot replace atomically; second best is below.
                    File.Delete(path);
                }
            }

            File.Move(temp, path);
        }

        private void SetAsideCorruptFile()
        {
            try
            {
                var aside = path + ".corrupt";

                if (File.Exists(aside))
                {
                    File.Delete(aside);
                }

                File.Move(path, aside);
            }
            catch (Exception)
            {
                // Then it will simply be overwritten by the next save.
            }
        }

        #endregion

        /// <summary>
        /// Stops the worker without waiting for a network call to time out, and saves what is left - held-back
        /// events included - so the next session can send it.
        /// </summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;

                try
                {
                    Properties.Settings.Default.PropertyChanged -= OnSettingChanged;
                }
                catch (Exception ex)
                {
                    logger.LogException(ex, this);
                }

                try
                {
                    ReleaseHeldBack(DateTime.UtcNow, ReadCooldown(), true);
                }
                catch (Exception ex)
                {
                    logger.LogException(ex, this);
                }

                Monitor.PulseAll(sync);
            }

            try
            {
                // MailKit aborts its socket from the cancellation callback, so a send in progress ends now rather
                // than after its 30 s timeout.
                stop.Cancel();
            }
            catch (Exception ex)
            {
                logger.LogException(ex, this);
            }

            var joined = false;

            try
            {
                wake.Set();
                joined = worker.Join(WORKER_JOIN_TIMEOUT);
            }
            catch (Exception ex)
            {
                logger.LogException(ex, this);
            }

            if (!joined)
            {
                logger.LogTracing("The email outbox worker did not stop in time; abandoning it.", LogLvl.LOG_WARNING, this);
            }

            PersistIfDirty();

            if (joined)
            {
                // Only once the worker is gone: it waits on both.
                wake.Dispose();
                stop.Dispose();
            }
        }
    }
}
