using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// Decides whether a play request is allowed to take a voice, given how recently and how often
    /// the same entry is already sounding.
    /// </summary>
    /// <remarks>
    /// <para><b>The play state lives here, never on the <c>AudioEntry</c> asset.</b> Writing a
    /// timestamp to a ScriptableObject is the obvious shortcut and it is a trap: the value survives
    /// leaving play mode in the Editor, is shared across every scene, and makes the second play
    /// session of the day behave differently from the first.</para>
    ///
    /// <para>The clock is a parameter rather than <c>Time.time</c>, so the service can feed its own
    /// accumulated time and the whole class stays a pure function of its inputs.</para>
    ///
    /// <para>Everything is keyed by the entry's <c>int</c> index rather than its string id, so the
    /// hot path costs an array index instead of a hash.</para>
    /// </remarks>
    public sealed class AudioFireRateGate
    {
        private const int SlotsPerEntry = 4;

        private float[] _lastPlayTime;
        private List<int>[] _activeSlots;

        public AudioFireRateGate(int entryCapacity)
        {
            int capacity = Math.Max(1, entryCapacity);
            this._lastPlayTime = new float[capacity];
            this._activeSlots = new List<int>[capacity];
            this.ResetTimestamps(0, capacity);
        }

        /// <summary>
        /// Reports whether <paramref name="entryIndex"/> may start another instance now, and fills
        /// <paramref name="slotsToStop"/> with the voices the caller must stop first.
        /// </summary>
        /// <remarks>
        /// <paramref name="slotsToStop"/> is cleared on entry, so a caller reusing one buffer can
        /// never leak a stale slot from the previous request into this one. It stays empty whenever
        /// the request is refused.
        /// </remarks>
        public bool TryAcquire(
            int entryIndex,
            float now,
            in AudioFireRateRule rule,
            List<int> slotsToStop,
            out AudioThrottleReason reason)
        {
            if (slotsToStop == null) throw new ArgumentNullException(nameof(slotsToStop));
            if (entryIndex < 0) throw new ArgumentOutOfRangeException(nameof(entryIndex));

            slotsToStop.Clear();
            reason = AudioThrottleReason.None;
            this.EnsureCapacity(entryIndex + 1);

            // Fire rate is checked before concurrency so a caller that is spamming hears about the
            // spam rather than about a cap that the spam happens to have filled.
            if (rule.MinIntervalSeconds > 0f
                && now - this._lastPlayTime[entryIndex] < rule.MinIntervalSeconds)
            {
                reason = AudioThrottleReason.FireRate;
                return false;
            }

            List<int> active = this._activeSlots[entryIndex];
            int activeCount = active?.Count ?? 0;

            // Restart is unconditional: the point of a single-instance sound is that asking for it
            // again replaces what is playing, whether or not a cap was reached.
            if (rule.Policy == AudioConcurrencyPolicy.Restart)
            {
                if (activeCount > 0)
                    slotsToStop.AddRange(active);

                return true;
            }

            if (rule.MaxConcurrent > 0 && activeCount >= rule.MaxConcurrent)
            {
                if (rule.Policy == AudioConcurrencyPolicy.StealOldest)
                {
                    // Insertion order is start order, so the head is the oldest instance.
                    slotsToStop.Add(active[0]);
                    return true;
                }

                reason = AudioThrottleReason.ConcurrencyLimit;
                return false;
            }

            return true;
        }

        /// <summary>Records that <paramref name="slot"/> is now sounding <paramref name="entryIndex"/>.</summary>
        public void NotifyStarted(int entryIndex, float now, int slot)
        {
            if (entryIndex < 0) throw new ArgumentOutOfRangeException(nameof(entryIndex));

            this.EnsureCapacity(entryIndex + 1);
            this._lastPlayTime[entryIndex] = now;

            List<int> active = this._activeSlots[entryIndex];
            if (active == null)
            {
                active = new List<int>(SlotsPerEntry);
                this._activeSlots[entryIndex] = active;
            }

            active.Add(slot);
        }

        /// <summary>
        /// Records that <paramref name="slot"/> has stopped, giving the entry its cap slot back.
        /// </summary>
        /// <remarks>
        /// Tolerates an unknown entry or slot: a voice can be stopped by teardown after the gate
        /// has already forgotten it, and that is not worth an exception.
        /// </remarks>
        public void NotifyStopped(int entryIndex, int slot)
        {
            if (entryIndex < 0 || entryIndex >= this._activeSlots.Length)
                return;

            this._activeSlots[entryIndex]?.Remove(slot);
        }

        /// <summary>Forgets every timestamp and live slot. For service teardown.</summary>
        public void Clear()
        {
            this.ResetTimestamps(0, this._lastPlayTime.Length);

            for (int i = 0; i < this._activeSlots.Length; i++)
                this._activeSlots[i]?.Clear();
        }

        private void EnsureCapacity(int required)
        {
            if (required <= this._lastPlayTime.Length)
                return;

            int grown = Math.Max(required, this._lastPlayTime.Length * 2);
            int previous = this._lastPlayTime.Length;

            Array.Resize(ref this._lastPlayTime, grown);
            Array.Resize(ref this._activeSlots, grown);
            this.ResetTimestamps(previous, grown);
        }

        /// <remarks>
        /// Negative infinity, not zero: a brand new entry must be playable at time 0, and a zero
        /// here would silently throttle the very first sound of the session.
        /// </remarks>
        private void ResetTimestamps(int from, int to)
        {
            for (int i = from; i < to; i++)
                this._lastPlayTime[i] = float.NegativeInfinity;
        }
    }
}
