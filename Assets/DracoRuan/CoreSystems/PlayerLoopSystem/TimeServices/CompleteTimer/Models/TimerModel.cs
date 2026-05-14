using System;
using System.Collections.Generic;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models
{
    public class TimerModel : IDisposable
    {
        public string TimerId;
        public long StartUnixTime;
        public int TierCount;
        
        private List<long> _ticksByTier;

        public TimerModel(string timerId, long startUnixTime, long duration)
        {
            this.TimerId = timerId;
            this.StartUnixTime = startUnixTime;
            this.TierCount = 1;
            this._ticksByTier = new List<long> { duration, };
        }

        public TimerModel(string timerId, long startUnixTime, params long[] durations)
        {
            this.TimerId = timerId;
            this.StartUnixTime = startUnixTime;
            this.TierCount = durations.Length;
            this._ticksByTier = new List<long>();
            this._ticksByTier.AddRange(durations);
        }
        
        public TimerModel(string timerId, long startUnixTime, List<long> durations)
        {
            this.TimerId = timerId;
            this.StartUnixTime = startUnixTime;
            this.TierCount = durations.Count;
            this._ticksByTier = new List<long>();
            this._ticksByTier.AddRange(durations);
        }

        public void Dispose()
        {
            this.TimerId = null;
            this._ticksByTier?.Clear();
            this._ticksByTier = null;
        }
    }
}
