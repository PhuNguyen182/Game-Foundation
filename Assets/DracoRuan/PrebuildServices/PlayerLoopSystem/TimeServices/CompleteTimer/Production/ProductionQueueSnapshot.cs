namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Production
{
    /// <summary>One pending item's persisted shape: id plus the duration it will run for once started.</summary>
    public struct ProductionQueueItemSnapshot
    {
        public string ItemId;
        public long DurationMs;
    }

    /// <summary>One finished-but-unclaimed output's persisted shape: id plus the absolute moment it finished.</summary>
    public struct ProductionQueueOutputSnapshot
    {
        public string ItemId;
        public long AtMs;
    }

    /// <summary>
    /// Persistence-facing snapshot of one <see cref="ProductionQueue"/>.
    /// </summary>
    /// <remarks>
    /// The active timer itself is not duplicated here - only its key, <see cref="ActiveTimerKey"/> -
    /// because the timer's own state already lives in a <see cref="Scheduling.TimerEntrySnapshot"/>
    /// captured by <see cref="Scheduling.TimerScheduler.CaptureSnapshot"/>. Restoring re-links the two
    /// via <see cref="Scheduling.TimerScheduler.TryGetHandle"/> rather than re-creating the timer.
    /// </remarks>
    public sealed class ProductionQueueSnapshot
    {
        public string QueueKey;
        public int Capacity;
        public int Channel;
        public ProductionQueueItemSnapshot[] Items;
        public ProductionQueueOutputSnapshot[] Outputs;
        public string ActiveTimerKey;
        public long Sequence;
        public bool IsPaused;
    }
}
