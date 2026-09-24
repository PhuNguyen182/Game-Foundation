namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>Receives <see cref="TimerEvent"/> notifications, either for a single timer or for a whole channel.</summary>
    public interface ITimerListener
    {
        /// <summary>
        /// Called for each dispatched or replayed event. Implementations may safely create, cancel
        /// or release timers on the scheduler from within this callback.
        /// </summary>
        void OnTimerEvent(in TimerEvent e);
    }
}
