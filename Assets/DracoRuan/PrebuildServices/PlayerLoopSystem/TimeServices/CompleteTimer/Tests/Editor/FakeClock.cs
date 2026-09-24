namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>
    /// Manually-driven clock for EditMode tests: both the device time and the monotonic source are
    /// set by hand, so tests can simulate offline gaps, clock rollbacks and deep-sleep freezes
    /// without any real waiting.
    /// </summary>
    internal sealed class FakeClock : Clock.ITimeProvider
    {
        public FakeClock(long startUtcMs = 0)
        {
            this.DeviceUtcMs = startUtcMs;
            this.MonotonicMs = 0;
            this.UtcNowMs = startUtcMs;
        }

        /// <summary>What the device clock currently reads; settable directly to simulate a user changing the clock.</summary>
        public long DeviceUtcMs { get; set; }

        /// <summary>What the monotonic source currently reads; settable directly to simulate deep sleep freezing it.</summary>
        public long MonotonicMs { get; set; }

        /// <summary>Directly-controlled "now" used by anything holding this as <see cref="Clock.ITimeProvider"/> (e.g. the scheduler under test).</summary>
        public long UtcNowMs { get; set; }

        /// <summary>Advances both device time and the reported now by the same amount - the common case of real elapsed time passing.</summary>
        public void Advance(long ms)
        {
            this.DeviceUtcMs += ms;
            this.MonotonicMs += ms;
            this.UtcNowMs += ms;
        }
    }
}
