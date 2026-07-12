using System.Collections.Generic;
using MemoryPack;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Models
{
    [MemoryPackable]
    public partial class TimerSaveData
    {
        public Dictionary<string, TimerSaveUnitModel> TimerSaveDataUnits = new();
    }
}