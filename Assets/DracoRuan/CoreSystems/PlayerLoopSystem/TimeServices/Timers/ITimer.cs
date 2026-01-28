using System;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.Timers
{
    public interface ITimer
    {
        public bool IsFinished { get; }
        public bool IsRunning { get; }
        public float CurrentTime { get; }
        public float Progress { get; }
        public Action OnTimerStart { get; set; }
        public Action<float> OnTimerUpdate { get; set; }
        public Action OnTimerStop { get; set; }

        public void Start();
        public void Stop();
        public void Resume();
        public void Pause();
        public void Reset();
        public void Reset(float newTime);
    }
}
