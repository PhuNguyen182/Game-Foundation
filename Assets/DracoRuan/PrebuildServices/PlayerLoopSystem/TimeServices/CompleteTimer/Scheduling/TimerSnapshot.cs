namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>
    /// Persistence-facing snapshot of one non-free timer slot.
    /// </summary>
    /// <remarks>
    /// This is the boundary between the pure-C# scheduler and the MessagePack save layer in
    /// Assembly-CSharp: the integration layer copies these into its own <c>[MessagePackObject]</c>
    /// entry class rather than the core referencing MessagePack directly (core stays
    /// <c>noEngineReferences</c> and dependency-free). Only absolute moments are stored - no
    /// "remaining" value - per the "store immutable moments, derive remaining" principle.
    /// <para>
    /// A struct, so capturing a snapshot into a reused <c>List</c> allocates nothing per entry.
    /// <see cref="StageEnds"/> is shared with the scheduler rather than copied: treat it as read-only.
    /// </para>
    /// </remarks>
    public struct TimerEntrySnapshot
    {
        public string Key;
        public int Channel;
        public TimerState State;
        public long StartMs;
        public long DurationMs;
        public long PausedAtMs;

        /// <summary>Cumulative stage-end offsets. Shared with the scheduler's record (capture) - never write into it.</summary>
        public long[] StageEnds;

        public int DispatchedStage;
        public bool AutoRelease;
        public long CompletedAtMs;
        public bool CompletedDelivered;
    }
}