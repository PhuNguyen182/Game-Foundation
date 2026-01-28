using System;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.Timers
{
    /// <summary>
    /// Timer that ticks at a specific frequency. (N times per second)
    /// </summary>
    public class FrequencyTimer : BaseTimer
    {
        private float _timeThreshold;
        
        public int TicksPerSecond { get; private set; }
        public override bool IsFinished => !this.IsRunning;

        public Action OnTick;

        public FrequencyTimer(int ticksPerSecond) : base(0) => this.CalculateTimeThreshold(ticksPerSecond);

        public override void Tick(float deltaTime)
        {
            if (this.IsRunning && this.CurrentTime >= this._timeThreshold)
            {
                this.CurrentTime -= this._timeThreshold;
                this.OnTimerUpdate?.Invoke(this.CurrentTime);
                this.OnTick?.Invoke();
            }

            if (this.IsRunning && this.CurrentTime < this._timeThreshold)
                this.CurrentTime += deltaTime;
        }
        
        public override void Reset() => this.CurrentTime = 0;
        
        public void Reset(int newTicksPerSecond)
        {
            this.CalculateTimeThreshold(newTicksPerSecond);
            this.Reset();
        }

        private void CalculateTimeThreshold(int ticksPerSecond)
        {
            this.TicksPerSecond = ticksPerSecond;
            this._timeThreshold = 1f / this.TicksPerSecond;
        }

        protected override void ReleaseAllCallbacks()
        {
            base.ReleaseAllCallbacks();
            this.OnTick = null;
        }
    }
}
