using System;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Regeneration
{
    /// <summary>
    /// Integer-tick regenerating resource (Lives/Energy/Stamina): whole units accrue at a fixed
    /// interval, with zero cumulative rounding error, even when consumed mid-interval.
    /// </summary>
    /// <remarks>
    /// <para><b>Why not multi-stage timers.</b> A multi-stage <see cref="Scheduling.TimerScheduler"/>
    /// timer models a fixed sequence that is never "spent" partway through (crop growth). Lives are
    /// consumed while a regen interval is only partially elapsed, and consuming must not reset that
    /// partial progress - which a stage-based timer has no way to express. This class instead tracks
    /// <see cref="AnchorMs"/>, the moment the *current* regen interval started, and folds completed
    /// intervals into <see cref="Stored"/> while carrying the remainder forward
    /// (<c>AnchorMs += n * IntervalMs</c>), so no fractional progress is ever discarded (see
    /// REWRITE_PLAN.md Q3/5.3).</para>
    ///
    /// <para>Only one <see cref="Scheduling.TimerScheduler"/> timer is used per counter - for the
    /// next single regen tick - regardless of how many units are missing, so an offline gap of any
    /// length resolves in O(1) rather than firing once per missed tick.</para>
    /// </remarks>
    public sealed class RegenerationCounter : ITimerListener
    {
        private readonly TimerScheduler _scheduler;
        private readonly string _timerKey;
        private readonly int _channel;

        private TimerHandle _timerHandle;

        public RegenerationCounter(TimerScheduler scheduler, string key, long stored, long max, long intervalMs,
            long anchorMs, int channel = 0)
        {
            this._scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.Key = key ?? throw new ArgumentNullException(nameof(key));
            this._timerKey = key + "/regen";
            this._channel = channel;

            this.Stored = stored;
            this.Max = max;
            this.IntervalMs = intervalMs;
            this.AnchorMs = anchorMs;
            this._timerHandle = TimerHandle.Invalid;
        }

        public string Key { get; }

        /// <summary>Whole units currently banked (not counting the in-progress interval).</summary>
        public long Stored { get; private set; }

        public long Max { get; private set; }

        public long IntervalMs { get; private set; }

        /// <summary>Absolute UTC ms moment the interval currently accruing toward the next unit began.</summary>
        public long AnchorMs { get; private set; }

        /// <summary>Raised after a regen timer fires and folds units into <see cref="Stored"/>: (gained, current, lastAtMs).</summary>
        public event Action<long, long, long> OnRegenerated;

        /// <summary>Current whole units available as of <paramref name="nowMs"/>, without mutating state.</summary>
        public long GetCurrent(long nowMs)
        {
            // Not clamped to Max here: Add(..., allowOverMax: true) can legitimately leave Stored
            // above Max (e.g. a reward pushing Lives past its cap), and that state must stay visible
            // to callers until consumption brings it back down - clamping the read would silently
            // hide units the player was actually given.
            if (this.Stored >= this.Max)
                return this.Stored;

            long elapsed = nowMs - this.AnchorMs;
            if (elapsed <= 0)
                return this.Stored;

            long gained = elapsed / this.IntervalMs;
            long current = this.Stored + gained;
            return current > this.Max ? this.Max : current;
        }

        /// <summary>Ms until the next single unit completes, or 0 if already full.</summary>
        public long GetNextRegenRemainingMs(long nowMs)
        {
            if (this.Stored >= this.Max)
                return 0;

            long elapsed = nowMs - this.AnchorMs;
            if (elapsed < 0)
                elapsed = 0;

            long intoInterval = elapsed % this.IntervalMs;
            return this.IntervalMs - intoInterval;
        }

        /// <summary>Ms until the counter is completely full, or 0 if already full.</summary>
        public long GetFullRemainingMs(long nowMs)
        {
            long missing = this.Max - this.GetCurrent(nowMs);
            if (missing <= 0)
                return 0;

            return this.GetNextRegenRemainingMs(nowMs) + (missing - 1) * this.IntervalMs;
        }

        /// <summary>
        /// Folds every whole interval elapsed since <see cref="AnchorMs"/> into <see cref="Stored"/>,
        /// keeping the remainder rather than discarding it, so no partial progress toward the next
        /// unit is ever lost.
        /// </summary>
        public void Normalize(long nowMs)
        {
            if (this.Stored >= this.Max)
            {
                this.AnchorMs = nowMs;
                return;
            }

            long elapsed = nowMs - this.AnchorMs;
            if (elapsed <= 0)
                return;

            long gained = elapsed / this.IntervalMs;
            if (gained <= 0)
                return;

            long allowedGain = this.Max - this.Stored;
            if (gained >= allowedGain)
            {
                this.Stored = this.Max;
                this.AnchorMs = nowMs;
            }
            else
            {
                this.Stored += gained;
                this.AnchorMs += gained * this.IntervalMs;
            }
        }

        /// <summary>
        /// Spends <paramref name="count"/> units. Normalizes first so any interval that already
        /// completed is credited before spending. Consuming while an interval is only partially
        /// elapsed does not reset that partial progress.
        /// </summary>
        public bool TryConsume(long count, long nowMs)
        {
            if (count <= 0)
                return false;

            this.Normalize(nowMs);

            if (this.Stored < count)
                return false;

            bool wasFull = this.Stored >= this.Max;
            this.Stored -= count;

            if (wasFull)
                this.AnchorMs = nowMs;

            this.RescheduleTimer(nowMs);
            return true;
        }

        /// <summary>Fills to <see cref="Max"/> immediately.</summary>
        public void Refill(long nowMs)
        {
            this.Stored = this.Max;
            this.AnchorMs = nowMs;
            this.CancelTimer();
        }

        /// <summary>
        /// Adds units directly (e.g. a reward), optionally exceeding <see cref="Max"/>. When over
        /// max, regeneration stops accruing further until consumption brings it back under max.
        /// </summary>
        public void Add(long count, long nowMs, bool allowOverMax = false)
        {
            this.Normalize(nowMs);

            this.Stored += count;
            if (!allowOverMax && this.Stored > this.Max)
                this.Stored = this.Max;

            if (this.Stored >= this.Max)
                this.AnchorMs = nowMs;

            this.RescheduleTimer(nowMs);
        }

        public void SetMax(long max, long nowMs)
        {
            this.Normalize(nowMs);
            this.Max = max;

            if (this.Stored > this.Max)
                this.Stored = this.Max;

            this.RescheduleTimer(nowMs);
        }

        /// <summary>Arms (or re-arms) the single scheduler timer for the next unit, if not already full.</summary>
        public void RescheduleTimer(long nowMs)
        {
            this.CancelTimer();

            if (this.Stored >= this.Max)
                return;

            long remaining = this.GetNextRegenRemainingMs(nowMs);
            if (remaining <= 0)
                remaining = this.IntervalMs;

            TimerSpec spec = new()
            {
                Key = this._timerKey,
                Channel = this._channel,
                DurationMs = remaining,
                // StartUtcMs + DurationMs must land exactly on AnchorMs + IntervalMs (the next
                // absolute regen boundary). remaining was computed from the same nowMs via
                // GetNextRegenRemainingMs, which floors against AnchorMs - never against nowMs
                // itself - so no drift is introduced regardless of which "now" is passed here, late
                // dispatch included: AnchorMs is what carries the absolute schedule.
                StartUtcMs = nowMs,
                AutoRelease = true
            };

            if (this._scheduler.TryStart(in spec, out TimerHandle handle))
            {
                this._timerHandle = handle;
                this._scheduler.SetListener(handle, this);
            }
        }

        private void CancelTimer()
        {
            if (this._timerHandle.IsValid)
            {
                this._scheduler.Cancel(this._timerHandle);
                this._timerHandle = TimerHandle.Invalid;
            }
        }

        void ITimerListener.OnTimerEvent(in TimerEvent e)
        {
            if (e.Type != TimerEventType.Completed)
                return;

            // Normalize against the scheduler's actual current time, not e.AtMs (the fired timer's
            // own theoretical deadline - just the *next* single unit). Using e.AtMs here silently
            // caps a catch-up to at most one interval's worth of gain, because Normalize only sees
            // elapsed = e.AtMs - AnchorMs, which is exactly one IntervalMs by construction. A 2-hour
            // offline gap with a 30-minute interval must fold all 4 missed intervals in one shot
            // (REWRITE_PLAN.md 5.3), which requires the real "now".
            long nowMs = this._scheduler.NowMs;
            long before = this.Stored;

            this._timerHandle = TimerHandle.Invalid;
            this.Normalize(nowMs);

            long gained = this.Stored - before;
            this.OnRegenerated?.Invoke(gained, this.Stored, nowMs);

            this.RescheduleTimer(nowMs);
        }

        internal RegenerationSnapshot Capture() => new()
        {
            Key = this.Key,
            Stored = this.Stored,
            Max = this.Max,
            IntervalMs = this.IntervalMs,
            AnchorMs = this.AnchorMs
        };

        internal void RestoreFrom(RegenerationSnapshot snapshot, long nowMs)
        {
            this.Stored = snapshot.Stored;
            this.Max = snapshot.Max;
            this.IntervalMs = snapshot.IntervalMs;
            this.AnchorMs = snapshot.AnchorMs;
            this.RescheduleTimer(nowMs);
        }
    }
}