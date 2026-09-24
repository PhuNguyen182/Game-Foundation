namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>
    /// Describes a timer to create via <see cref="TimerScheduler.TryStart"/>.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="DurationMs"/> or <see cref="StageDurationsMs"/> should be set; a
    /// single duration is the N = 1 case of the multi-stage model (see REWRITE_PLAN.md Q5). The
    /// scheduler copies whichever is given into cumulative stage-end offsets at creation time, so
    /// mutating the array afterward (or changing a shared config array) never retroactively affects
    /// an already-started timer.
    /// </remarks>
    public struct TimerSpec
    {
        /// <summary>Unique key identifying this timer. <see cref="TimerScheduler.TryStart"/> fails if this key already exists.</summary>
        public string Key;

        /// <summary>Logical channel used to route events to a shared <see cref="ITimerListener"/> (e.g. "crop", "building").</summary>
        public int Channel;

        /// <summary>Single-stage duration in ms. Ignored if <see cref="StageDurationsMs"/> is set.</summary>
        public long DurationMs;

        /// <summary>Ordered per-stage durations in ms for a multi-stage timer (see REWRITE_PLAN.md Q5).</summary>
        public long[] StageDurationsMs;

        /// <summary>
        /// Start time override in absolute UTC ms. Null means "now". Used by
        /// <see cref="Production.ProductionQueue"/> to chain the next item's start to the previous
        /// item's completion moment rather than to the current wall-clock time, so offline catch-up
        /// preserves exact timing.
        /// </summary>
        public long? StartUtcMs;

        /// <summary>
        /// When true, the scheduler releases this timer automatically once its <c>Completed</c>
        /// event has been delivered to at least one listener (see REWRITE_PLAN.md Q4). Leave false
        /// for timers whose completion must be claimed by gameplay before disappearing.
        /// </summary>
        public bool AutoRelease;
    }
}
