using System.Collections.Generic;
using MemoryPack;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models
{
    [MemoryPackable]
    public partial class TimerModelCollection
    {
        public Dictionary<string, TimerModel> TimerModels = new();
    }
}
