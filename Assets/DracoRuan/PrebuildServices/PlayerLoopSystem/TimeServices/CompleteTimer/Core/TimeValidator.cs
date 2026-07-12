using System;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.Extensions;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimeValidator
    {
        public DateTime CurrentTime => TimeExtensions.CurrentUtcTime;

        public long CurrentUnixTimestamp() => TimeExtensions.GetCurrentUtcTimestampInMilliseconds();
    }
}
