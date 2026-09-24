using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>Records every <see cref="TimerEvent"/> it receives, in delivery order, for assertions.</summary>
    internal sealed class TestListener : ITimerListener
    {
        public readonly List<TimerEvent> Events = new();

        public void OnTimerEvent(in TimerEvent e) => this.Events.Add(e);

        public void Clear() => this.Events.Clear();
    }
}
