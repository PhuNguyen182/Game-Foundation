namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock
{
    /// <summary>Kind of unexpected jump detected between the device clock and the anchored clock.</summary>
    public enum ClockAnomalyKind
    {
        /// <summary>Device clock moved backward relative to the last seen time (user set clock back).</summary>
        Backward,

        /// <summary>Device clock jumped forward by more than the drift tolerance (e.g. after deep sleep).</summary>
        ForwardJump
    }

    /// <summary>
    /// Describes one detected clock anomaly: what kind it was and by how much the device time
    /// disagreed with the clock's own anchored time.
    /// </summary>
    public readonly struct ClockAnomaly
    {
        public ClockAnomaly(ClockAnomalyKind kind, long deltaMs)
        {
            this.Kind = kind;
            this.DeltaMs = deltaMs;
        }

        /// <summary>Which kind of anomaly this was.</summary>
        public ClockAnomalyKind Kind { get; }

        /// <summary>
        /// Magnitude of the disagreement in milliseconds. Always non-negative: for
        /// <see cref="ClockAnomalyKind.Backward"/> it is how far behind the device clock fell; for
        /// <see cref="ClockAnomalyKind.ForwardJump"/> it is how far ahead it jumped.
        /// </summary>
        public long DeltaMs { get; }
    }
}
