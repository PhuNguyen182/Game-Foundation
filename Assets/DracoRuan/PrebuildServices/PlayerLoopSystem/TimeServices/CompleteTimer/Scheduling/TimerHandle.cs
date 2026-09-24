using System;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>
    /// Lightweight reference to one timer slot inside <see cref="TimerScheduler"/>.
    /// </summary>
    /// <remarks>
    /// A struct so holding thousands of handles (e.g. one per UI row) never allocates. <see cref="Version"/>
    /// is what makes a stale handle safe: when a slot is released and reused for a new timer, its
    /// version is bumped, so any handle captured before the release compares unequal to the slot's
    /// current version and every query on it returns a default rather than reading someone else's
    /// timer.
    /// </remarks>
    public readonly struct TimerHandle : IEquatable<TimerHandle>
    {
        public static readonly TimerHandle Invalid = new(-1, 0);

        public TimerHandle(int index, int version)
        {
            this.Index = index;
            this.Version = version;
        }

        /// <summary>Slot index inside the scheduler's internal record array.</summary>
        public int Index { get; }

        /// <summary>Generation of the slot at the time this handle was issued.</summary>
        public int Version { get; }

        /// <summary>False for <see cref="Invalid"/> or any default-constructed handle.</summary>
        public bool IsValid => this.Index >= 0;

        public bool Equals(TimerHandle other) => this.Index == other.Index && this.Version == other.Version;

        public override bool Equals(object obj) => obj is TimerHandle other && this.Equals(other);

        public override int GetHashCode() => (this.Index * 397) ^ this.Version;

        public static bool operator ==(TimerHandle left, TimerHandle right) => left.Equals(right);

        public static bool operator !=(TimerHandle left, TimerHandle right) => !left.Equals(right);
    }
}
