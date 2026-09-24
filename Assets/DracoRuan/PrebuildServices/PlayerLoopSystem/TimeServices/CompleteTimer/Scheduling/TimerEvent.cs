namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>
    /// One notification dispatched by <see cref="TimerScheduler"/>: a stage advance or a completion.
    /// </summary>
    /// <remarks>
    /// <see cref="AtMs"/> is always the theoretical moment the event happened (the stage boundary or
    /// final deadline), never the moment it was actually dispatched - offline catch-up can deliver an
    /// event long after its <see cref="AtMs"/>, which is exactly what <see cref="IsLate"/> flags.
    /// </remarks>
    public readonly struct TimerEvent
    {
        public TimerEvent(
            TimerHandle handle, string key, int channel, TimerEventType type, int stage, long atMs,
            bool isLate, bool isReplay)
        {
            this.Handle = handle;
            this.Key = key;
            this.Channel = channel;
            this.Type = type;
            this.Stage = stage;
            this.AtMs = atMs;
            this.IsLate = isLate;
            this.IsReplay = isReplay;
        }

        /// <summary>Handle of the timer this event belongs to.</summary>
        public TimerHandle Handle { get; }

        /// <summary>Key of the timer this event belongs to.</summary>
        public string Key { get; }

        /// <summary>Channel of the timer this event belongs to.</summary>
        public int Channel { get; }

        /// <summary>Whether this is a stage advance or the final completion.</summary>
        public TimerEventType Type { get; }

        /// <summary>
        /// Stage this event refers to. For <see cref="TimerEventType.StageChanged"/> this is the
        /// stage just entered (1-based among the middle stages); for <see cref="TimerEventType.Completed"/>
        /// this is the total stage count N.
        /// </summary>
        public int Stage { get; }

        /// <summary>Theoretical UTC ms moment this event happened, independent of when it was dispatched.</summary>
        public long AtMs { get; }

        /// <summary>True when dispatched more than ~1000 ms after <see cref="AtMs"/> (offline catch-up).</summary>
        public bool IsLate { get; }

        /// <summary>True when this is a re-delivery of a previously undelivered or completed event, not its first dispatch.</summary>
        public bool IsReplay { get; }
    }
}
