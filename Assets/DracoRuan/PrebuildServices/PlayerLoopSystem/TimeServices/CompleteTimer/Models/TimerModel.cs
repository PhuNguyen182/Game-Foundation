using System;
using System.Collections.Generic;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models
{
    public class TimerModel : IDisposable
    {
        public readonly long StartUnixTime;

        public string TimerId { get; private set; }
        public int TimerTierCount => this.TicksByTier?.Count ?? 0; 
        public List<long> TicksByTier { get; private set; }

        public TimerModel(string timerId, long startUnixTime, long duration)
        {
            this.TimerId = timerId;
            this.StartUnixTime = startUnixTime;
            this.TicksByTier = new List<long> { duration, };
        }

        public TimerModel(string timerId, long startUnixTime, List<long> durations)
        {
            this.TimerId = timerId;
            this.StartUnixTime = startUnixTime;
            this.TicksByTier = new List<long>(durations);
        }

        public void Dispose()
        {
            this.TimerId = null;
            this.TicksByTier?.Clear();
            this.TicksByTier = null;
        }
    }
}
