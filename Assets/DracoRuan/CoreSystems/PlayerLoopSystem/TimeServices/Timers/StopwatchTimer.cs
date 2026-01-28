namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.Timers
{
    /// <summary>
    /// Timer that counts up from zero to infinity.  Great for measuring durations.
    /// </summary>
    public class StopwatchTimer : BaseTimer
    {
        public override bool IsFinished => false;
        
        public StopwatchTimer(float time) : base(time)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (!this.IsRunning) 
                return;
            
            this.CurrentTime += deltaTime;
            this.OnTimerUpdate?.Invoke(this.CurrentTime);
        }
    }
}
