using System;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.Extensions;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimeValidator
    {
        public DateTime CurrentTime => TimeExtensions.CurrentUtcTime;

        public long CurrentUnixTimestamp() => TimeExtensions.GetCurrentUtcTimestampInMilliseconds();
    }
}
