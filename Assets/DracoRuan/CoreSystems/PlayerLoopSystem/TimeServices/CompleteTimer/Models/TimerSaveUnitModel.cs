using System.Collections.Generic;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models
{
    public class TimerSaveUnitModel
    {
        public string TimerId;
        public long StartUnixTime;
        public int TierCount;
        public List<long> TicksByTier = new();
    }
}
