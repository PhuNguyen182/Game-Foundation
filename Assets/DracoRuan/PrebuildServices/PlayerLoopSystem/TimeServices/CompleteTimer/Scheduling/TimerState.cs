namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>Lifecycle state of one timer slot. See REWRITE_PLAN.md section 4/Q4 for the full lifecycle.</summary>
    public enum TimerState
    {
        /// <summary>Slot is unused and sits in the scheduler's free list.</summary>
        Free,

        /// <summary>Timer is counting down toward its next stage or completion deadline.</summary>
        Running,

        /// <summary>Timer is frozen at <c>PausedAtMs</c>; not present in the heap.</summary>
        Paused,

        /// <summary>All stages have elapsed. Stays in this state until <c>Release</c> frees the slot.</summary>
        Completed
    }
}
