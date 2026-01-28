namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.Timers
{
    /// <summary>
    /// Timer that counts down from a specific value to zero.
    /// </summary>
    public class CountdownTimer : BaseTimer
    {
        public override bool IsFinished => this.CurrentTime <= 0;

        public CountdownTimer(float time) : base(time)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (this.IsRunning && this.CurrentTime > 0)
            {
                this.CurrentTime -= deltaTime;
                this.OnTimerUpdate?.Invoke(this.CurrentTime);
            }

            if (this.IsRunning && this.CurrentTime <= 0)
                this.Stop();
        }
    }
}
