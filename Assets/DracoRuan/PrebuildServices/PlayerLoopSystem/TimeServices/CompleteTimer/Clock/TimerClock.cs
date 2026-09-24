using System;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock
{
    /// <summary>
    /// Anchored clock: <see cref="UtcNowMs"/> advances by the monotonic source, not by re-reading the
    /// device clock every call, so a mid-session clock change on the device never makes timers jump.
    /// </summary>
    /// <remarks>
    /// <para><b>Why anchor at all.</b> If every query read <c>DateTime.UtcNow</c> directly, a player
    /// dragging their device clock forward or backward while the app is running would instantly move
    /// every timer's remaining time. Anchoring to a monotonic source (a stopwatch that only ever
    /// counts up in real, unpaused wall-clock time) and re-deriving UTC from it means the device
    /// clock only matters at the moments this clock explicitly resyncs against it.</para>
    ///
    /// <para><b>Deep sleep is the exception.</b> On mobile, the monotonic source itself can stop
    /// counting while the app is suspended (Android's elapsed-realtime-while-awake family, or a
    /// naive <see cref="System.Diagnostics.Stopwatch"/> under aggressive OS suspension). That is
    /// exactly what <see cref="ReanchorFromDevice"/> and the per-tick drift check exist to correct:
    /// they compare the anchored time against a fresh device read and, if they disagree by more than
    /// <see cref="DriftToleranceMs"/>, re-anchor to the device's current time so offline progress is
    /// not lost to a frozen monotonic counter.</para>
    /// </remarks>
    public sealed class TimerClock : ITimeProvider
    {
        /// <summary>Default tolerance before a forward disagreement between device and anchor is treated as an anomaly.</summary>
        public const long DefaultDriftToleranceMs = 2000;

        private readonly Func<long> _deviceUtcMs;
        private readonly Func<long> _monotonicMs;

        private long _anchorUtcMs;
        private long _anchorMonoMs;
        private long _lastSeenUtcMs;

        /// <summary>
        /// Creates a clock anchored to the given device/monotonic sources.
        /// </summary>
        /// <param name="deviceUtcMs">Reads the device's current UTC time in ms. Defaults to <see cref="DateTime.UtcNow"/>.</param>
        /// <param name="monotonicMs">Reads a monotonically increasing ms counter. Defaults to a <see cref="System.Diagnostics.Stopwatch"/>.</param>
        public TimerClock(Func<long> deviceUtcMs = null, Func<long> monotonicMs = null)
        {
            this._deviceUtcMs = deviceUtcMs ?? DefaultDeviceUtcMs;
            this._monotonicMs = monotonicMs ?? CreateDefaultMonotonic();

            this._anchorUtcMs = this._deviceUtcMs();
            this._anchorMonoMs = this._monotonicMs();
            this._lastSeenUtcMs = this._anchorUtcMs;
        }

        /// <summary>How far the device clock may run ahead of the anchor before it is treated as a forward-jump anomaly.</summary>
        public long DriftToleranceMs { get; set; } = DefaultDriftToleranceMs;

        /// <summary>True once <see cref="SyncWithServer"/> has anchored to a trusted server time and no un-resynced focus regain has happened since.</summary>
        public bool IsServerSynced { get; private set; }

        /// <summary>
        /// Current time, anchored: device clock changes mid-session never move this value directly.
        /// </summary>
        public long UtcNowMs => this._anchorUtcMs + (this._monotonicMs() - this._anchorMonoMs);

        /// <summary>Raised when a backward or forward clock anomaly is detected.</summary>
        public event Action<ClockAnomaly> OnAnomaly;

        /// <summary>
        /// Raised when the clock was server-synced but regained focus without a fresh server read,
        /// so callers should treat elapsed time since as provisional until they resync.
        /// </summary>
        public event Action OnResyncRequired;

        /// <summary>
        /// Re-anchors to a trusted server timestamp. Call this whenever an authoritative time arrives
        /// from the backend.
        /// </summary>
        public void SyncWithServer(long serverUtcMs)
        {
            this._anchorUtcMs = serverUtcMs;
            this._anchorMonoMs = this._monotonicMs();
            this.IsServerSynced = true;

            if (serverUtcMs > this._lastSeenUtcMs)
                this._lastSeenUtcMs = serverUtcMs;
        }

        /// <summary>
        /// Re-anchors from the device clock. Call on regaining application focus, since the
        /// monotonic source may have frozen while suspended.
        /// </summary>
        /// <remarks>
        /// A device clock that appears to have moved backward is never adopted: the anchor is left
        /// untouched (time stands still rather than running backward) and <see cref="OnAnomaly"/>
        /// fires with <see cref="ClockAnomalyKind.Backward"/>. A forward move is always adopted,
        /// since that is the normal case (real elapsed time, including deep sleep); it also emits
        /// <see cref="ClockAnomaly"/> when it exceeds <see cref="DriftToleranceMs"/> purely as an
        /// informational signal, not to reject the reading.
        /// </remarks>
        public void ReanchorFromDevice()
        {
            long deviceNow = this._deviceUtcMs();
            long currentAnchored = this.UtcNowMs;

            if (deviceNow < this._lastSeenUtcMs)
            {
                this.OnAnomaly?.Invoke(new ClockAnomaly(ClockAnomalyKind.Backward, this._lastSeenUtcMs - deviceNow));
            }
            else
            {
                long delta = deviceNow - currentAnchored;
                if (delta > this.DriftToleranceMs)
                    this.OnAnomaly?.Invoke(new ClockAnomaly(ClockAnomalyKind.ForwardJump, delta));

                this._anchorUtcMs = deviceNow;
                this._anchorMonoMs = this._monotonicMs();
                this._lastSeenUtcMs = deviceNow;
            }

            if (this.IsServerSynced)
            {
                this.IsServerSynced = false;
                this.OnResyncRequired?.Invoke();
            }
        }

        /// <summary>
        /// Checks device/anchor drift without requiring a focus event. Intended to be called once per
        /// tick by the scheduler so a frozen monotonic source (deep sleep without a focus-lost event)
        /// is still caught.
        /// </summary>
        public void CheckDrift()
        {
            long deviceNow = this._deviceUtcMs();
            long currentAnchored = this.UtcNowMs;
            long delta = deviceNow - currentAnchored;

            if (delta > this.DriftToleranceMs)
                this.ReanchorFromDevice();
            else if (deviceNow > this._lastSeenUtcMs)
                this._lastSeenUtcMs = deviceNow;
        }

        /// <summary>
        /// Floors <see cref="UtcNowMs"/> at load time so a save written under a since-rolled-back
        /// device clock never appears to run backward once restored.
        /// </summary>
        public void ApplyFloor(long lastSeenUtcMs)
        {
            if (lastSeenUtcMs <= this._lastSeenUtcMs)
                return;

            this._lastSeenUtcMs = lastSeenUtcMs;

            if (lastSeenUtcMs <= this.UtcNowMs)
                return;

            // The persisted floor is ahead of what this fresh anchor computes (e.g. the device clock
            // was set forward then back before this session started) - jump the anchor up to the
            // floor so timers never appear to run backward relative to what was already observed.
            this._anchorUtcMs = lastSeenUtcMs;
            this._anchorMonoMs = this._monotonicMs();
        }

        /// <summary>Highest UTC ms ever observed by this clock; used to persist <see cref="ApplyFloor"/>'s input.</summary>
        public long LastSeenUtcMs => Math.Max(this._lastSeenUtcMs, this.UtcNowMs);

        private static long DefaultDeviceUtcMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private static Func<long> CreateDefaultMonotonic()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            return () => stopwatch.ElapsedMilliseconds;
        }
    }
}
