using System;
using System.Collections.Generic;

namespace NutDesk.Notifications
{
    /// <summary>
    /// An email rendered when its event happened, not yet in the outbox.
    /// </summary>
    /// <remarks>
    /// Kept in two parts so a note can be slipped in after the opening sentence - where it is read - rather than
    /// after the footer.
    /// </remarks>
    internal sealed class EmailDraft
    {
        internal EmailDraft(NotificationKind kind, DateTime eventUtc, string subject, string lead, string details)
        {
            Kind = kind;
            EventUtc = eventUtc;
            Subject = subject ?? string.Empty;
            Lead = lead ?? string.Empty;
            Details = details ?? string.Empty;
        }

        internal NotificationKind Kind { get; }

        internal DateTime EventUtc { get; }

        internal string Subject { get; }

        /// <summary>The sentence describing the event.</summary>
        internal string Lead { get; }

        /// <summary>The details block and the footer.</summary>
        internal string Details { get; }

        internal string ComposeBody(string note)
        {
            var paragraph = Environment.NewLine + Environment.NewLine;

            return string.IsNullOrEmpty(note)
                ? Lead + paragraph + Details
                : Lead + paragraph + note + paragraph + Details;
        }
    }

    /// <summary>
    /// Decides when an email may go into the outbox. Not thread-safe: the outbox calls it under its own lock.
    /// </summary>
    /// <remarks>
    /// Why this rule, and not a plain "at most one email per kind every N minutes":
    /// a flapping mains supply switches the UPS between battery and mains every few seconds, and nobody wants thirty
    /// emails about it - so within the cooldown only the first event of a kind is sent at once and the later ones
    /// wait. But simply dropping the later ones could leave the last email the user received saying "power
    /// restored" while the UPS is in fact on battery again. So the most recent held-back event of each kind is kept
    /// and sent when the cooldown ends, unless an event of the opposite kind has happened since, in which case it no
    /// longer describes the present and is dropped. Whatever the sequence, the last email of the pair always matches
    /// the last thing that happened.
    /// </remarks>
    internal sealed class EmailThrottle
    {
        private sealed class KindState
        {
            /// <summary>When the last email of this kind went into the outbox: the start of its cooldown.</summary>
            internal DateTime? LastQueuedUtc;

            /// <summary>When the latest event of this kind happened, whether it was queued or held back.</summary>
            internal DateTime? LastEventUtc;

            /// <summary>The most recent event held back by the cooldown.</summary>
            internal EmailDraft Pending;

            /// <summary>
            /// Events of this kind held back and not yet mentioned in any email. Whichever email of the pair goes
            /// out next owns them: it either is the last of them, or it replaced them.
            /// </summary>
            internal int Held;
        }

        private readonly Dictionary<NotificationKind, KindState> states = new Dictionary<NotificationKind, KindState>();

        /// <summary>A draft ready for the outbox, and how many events it stands in for that were never sent.</summary>
        internal struct Release
        {
            internal EmailDraft Draft;

            /// <summary>
            /// Held-back events of this kind and of its opposite that will never be sent on their own. Counting the
            /// opposite matters: after "power lost" and "power restored", a second "power restored" with no word
            /// about the blackout in between would read as a duplicate.
            /// </summary>
            internal int UnsentCount;
        }

        internal bool HasPending
        {
            get
            {
                foreach (var state in states.Values)
                {
                    if (state.Pending != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Offers a new event. Returns true when it may be queued right away (with <paramref name="release"/> to
        /// queue), false when it is held back until its cooldown expires.
        /// </summary>
        internal bool Offer(EmailDraft draft, DateTime nowUtc, TimeSpan cooldown, out Release release)
        {
            var state = StateOf(draft.Kind);

            if (!state.LastEventUtc.HasValue || draft.EventUtc > state.LastEventUtc.Value)
            {
                state.LastEventUtc = draft.EventUtc;
            }

            if (IsCoolingDown(state, nowUtc, cooldown))
            {
                state.Held++;
                state.Pending = draft;
                release = default(Release);
                return false;
            }

            // Out of the cooldown. A held-back event still here (the worker has not got round to it, or the
            // cooldown was shortened) is older than this one and is summed up by it; so is anything the opposite
            // kind held back, which is older too and will be dropped as stale.
            state.Pending = null;
            release = new Release { Draft = draft, UnsentCount = ClaimHeld(draft.Kind, state) };
            state.LastQueuedUtc = nowUtc;
            return true;
        }

        /// <summary>
        /// Takes out every held-back event whose cooldown has expired - or all of them when
        /// <paramref name="releaseAll"/> is set, because at shutdown there is no later. Stale events are dropped and
        /// reported through <paramref name="onStale"/>.
        /// </summary>
        internal List<Release> TakeDue(DateTime nowUtc, TimeSpan cooldown, bool releaseAll, Action<EmailDraft> onStale)
        {
            var due = new List<Release>();

            foreach (var entry in states)
            {
                var state = entry.Value;

                if (state.Pending == null || (!releaseAll && IsCoolingDown(state, nowUtc, cooldown)))
                {
                    continue;
                }

                var pending = state.Pending;
                state.Pending = null;

                var opposite = NotificationKinds.Opposite(entry.Key);

                if (opposite != NotificationKind.None
                    && states.TryGetValue(opposite, out var oppositeState)
                    && oppositeState.LastEventUtc.HasValue
                    && oppositeState.LastEventUtc.Value > pending.EventUtc)
                {
                    // Its count stays: the newer opposite email that makes it stale is the one that will mention it.
                    onStale?.Invoke(pending);
                    continue;
                }

                // The pending event itself is being sent, so it is not one of the unsent.
                state.Held = Math.Max(0, state.Held - 1);
                due.Add(new Release { Draft = pending, UnsentCount = ClaimHeld(entry.Key, state) });
                state.LastQueuedUtc = nowUtc;
            }

            // Oldest first, so the outbox sends them in the order they happened.
            due.Sort((a, b) => a.Draft.EventUtc.CompareTo(b.Draft.EventUtc));
            return due;
        }

        /// <summary>When the next held-back event becomes due, or null when nothing is held back.</summary>
        internal DateTime? NextDueUtc(TimeSpan cooldown)
        {
            DateTime? next = null;

            foreach (var state in states.Values)
            {
                if (state.Pending == null)
                {
                    continue;
                }

                var due = state.LastQueuedUtc.HasValue ? state.LastQueuedUtc.Value + cooldown : DateTime.MinValue;

                if (!next.HasValue || due < next.Value)
                {
                    next = due;
                }
            }

            return next;
        }

        /// <summary>Takes the held counts of a kind and of its opposite, for the email about to be queued.</summary>
        private int ClaimHeld(NotificationKind kind, KindState state)
        {
            var count = state.Held;
            state.Held = 0;

            var opposite = NotificationKinds.Opposite(kind);
            if (opposite != NotificationKind.None && states.TryGetValue(opposite, out var oppositeState))
            {
                count += oppositeState.Held;
                oppositeState.Held = 0;
            }

            return count;
        }

        private static bool IsCoolingDown(KindState state, DateTime nowUtc, TimeSpan cooldown)
        {
            return cooldown > TimeSpan.Zero
                && state.LastQueuedUtc.HasValue
                && nowUtc - state.LastQueuedUtc.Value < cooldown
                // A clock set backwards must not freeze a kind for as long as it went back.
                && nowUtc >= state.LastQueuedUtc.Value;
        }

        private KindState StateOf(NotificationKind kind)
        {
            if (!states.TryGetValue(kind, out var state))
            {
                state = new KindState();
                states[kind] = state;
            }

            return state;
        }
    }
}
