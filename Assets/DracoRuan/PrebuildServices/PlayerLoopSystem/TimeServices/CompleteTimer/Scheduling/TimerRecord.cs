namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>
    /// Internal per-slot state backing one <see cref="TimerHandle"/>.
    /// </summary>
    /// <remarks>
    /// A class (not a struct) so it can be stored by reference in the heap and referenced from the
    /// key lookup without copying; the array of records is pre-sized and slots are reused via the
    /// free list, so this allocates only when growing capacity, never per-timer.
    /// </remarks>
    internal sealed class TimerRecord
    {
        /// <summary>Key given at creation; cleared to null while the slot is <see cref="TimerState.Free"/>.</summary>
        public string Key;

        /// <summary>Channel given at creation.</summary>
        public int Channel;

        /// <summary>Current lifecycle state of this slot.</summary>
        public TimerState State;

        /// <summary>Absolute UTC ms the timer started; immutable except for <see cref="TimerScheduler.Resume"/>/<see cref="TimerScheduler.SpeedUp"/>/<see cref="TimerScheduler.Delay"/> shifting it.</summary>
        public long StartMs;

        /// <summary>
        /// Cumulative stage-end offsets from <see cref="StartMs"/>. Index k holds the offset at which
        /// stage k+1 begins (equivalently, stage k ends). Length N means the timer has N stages;
        /// <c>StageEnds[N-1]</c> is the total duration.
        /// </summary>
        public long[] StageEnds;

        /// <summary>Absolute UTC ms at which the timer was paused; meaningless unless <see cref="State"/> is <see cref="TimerState.Paused"/>.</summary>
        public long PausedAtMs;

        /// <summary>
        /// Last stage index (1-based, i.e. number of stage transitions already dispatched) for which
        /// an event has been dispatched. Only used to know what to dispatch next; querying the
        /// *current* displayed stage is always computed fresh from now (see 6.1.2).
        /// </summary>
        public int DispatchedStage;

        /// <summary>Absolute UTC ms of the next stage/completion boundary; this is the heap's sort key while <see cref="State"/> is Running.</summary>
        public long NextDeadlineMs;

        /// <summary>Monotonically increasing counter used to order equal-deadline entries by their creation order.</summary>
        public long Sequence;

        /// <summary>Current position of this record inside the min-heap's backing array, or -1 if not in the heap.</summary>
        public int HeapIndex;

        /// <summary>Generation of this slot; bumped every time it is released back to the free list.</summary>
        public int Version;

        /// <summary>Per-timer listener, checked before the channel's listener when dispatching.</summary>
        public ITimerListener Listener;

        /// <summary>Whether the scheduler should auto-release this timer once its Completed event has been delivered.</summary>
        public bool AutoRelease;

        /// <summary>UTC ms this timer completed at, once <see cref="State"/> is <see cref="TimerState.Completed"/>; used for the completed-age warning.</summary>
        public long CompletedAtMs;

        /// <summary>True once the Completed event for this timer has been handed to at least one listener.</summary>
        public bool CompletedDelivered;

        /// <summary>Resets a freed slot back to defaults so a stale reference cannot see leftover data.</summary>
        public void Reset()
        {
            this.Key = null;
            this.Channel = 0;
            this.State = TimerState.Free;
            this.StartMs = 0;
            this.StageEnds = null;
            this.PausedAtMs = 0;
            this.DispatchedStage = 0;
            this.NextDeadlineMs = 0;
            this.Sequence = 0;
            this.HeapIndex = -1;
            this.Listener = null;
            this.AutoRelease = false;
            this.CompletedAtMs = 0;
            this.CompletedDelivered = false;
        }
    }
}
