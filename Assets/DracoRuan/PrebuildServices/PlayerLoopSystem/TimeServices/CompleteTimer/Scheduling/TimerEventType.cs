namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>Kind of notification carried by a <see cref="TimerEvent"/>.</summary>
    public enum TimerEventType
    {
        /// <summary>Timer advanced from one stage to the next (but has not finished all stages).</summary>
        StageChanged,

        /// <summary>Timer finished its final stage.</summary>
        Completed
    }
}
