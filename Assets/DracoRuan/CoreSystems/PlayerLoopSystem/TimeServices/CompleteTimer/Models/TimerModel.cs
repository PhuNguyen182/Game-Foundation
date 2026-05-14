using System;
using System.Collections.Generic;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models
{
    public class TimerModel : IDisposable
    {
        public readonly long StartUnixTime;
        public readonly int TierCount;
        
        public string TimerId { get; private set; }
        public List<long> TicksByTier {get; private set;}

        public TimerModel(string timerId, long startUnixTime, long duration)
        {
            this.TimerId = timerId;
            this.StartUnixTime = startUnixTime;
            this.TierCount = 1;
            this.TicksByTier = new List<long> { duration, };
        }

        public TimerModel(string timerId, long startUnixTime, params long[] durations)
        {
            this.TimerId = timerId;
            this.StartUnixTime = startUnixTime;
            this.TierCount = durations.Length;
            this.TicksByTier = new List<long>();
            this.TicksByTier.AddRange(durations);
        }
        
        public TimerModel(string timerId, long startUnixTime, List<long> durations)
        {
            this.TimerId = timerId;
            this.StartUnixTime = startUnixTime;
            this.TierCount = durations.Count;
            this.TicksByTier = new List<long>();
            this.TicksByTier.AddRange(durations);
        }

        public void Dispose()
        {
            this.TimerId = null;
            this.TicksByTier?.Clear();
            this.TicksByTier = null;
        }
    }
}
