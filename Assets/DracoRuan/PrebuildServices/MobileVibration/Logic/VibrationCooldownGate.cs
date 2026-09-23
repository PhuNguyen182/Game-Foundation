using System;

namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>
    /// Decides whether a play request for a vibration entry is allowed, given how recently the same
    /// entry last played.
    /// </summary>
    /// <remarks>
    /// <para>The play state lives here, never on the <c>VibrationEntry</c> asset, for the same reason
    /// <c>AudioFireRateGate</c> keeps its own state off <c>AudioEntry</c>: a timestamp written to a
    /// ScriptableObject survives leaving play mode in the Editor, is shared across every scene, and
    /// would make the second play session of the day behave differently from the first.</para>
    ///
    /// <para>Only the "minimum interval" half of <c>AudioFireRateGate</c> survives here. There is no
    /// "max concurrent" half: the plugin has exactly one playback slot
    /// (<c>MOST_HapticFeedback._activePlayback</c>), so there is nothing to count concurrently — a
    /// second <c>Play</c> simply replaces whatever is running, the way the plugin's own
    /// <c>Generate</c> methods behave.</para>
    ///
    /// <para>The clock is a parameter rather than <c>Time.time</c>, so the service can feed its own
    /// accumulated time and this class stays a pure function of its inputs. Keyed by the entry's
    /// <c>int</c> index rather than its string id, so the hot path costs an array index instead of a
    /// hash.</para>
    /// </remarks>
    public sealed class VibrationCooldownGate
    {
        private float[] _lastPlayTime;

        public VibrationCooldownGate(int entryCapacity)
        {
            int capacity = Math.Max(1, entryCapacity);
            this._lastPlayTime = new float[capacity];
            this.ResetTimestamps(0, capacity);
        }

        /// <summary>
        /// Reports whether <paramref name="entryIndex"/> may play now, and if so, records the play so
        /// the next call is measured from it.
        /// </summary>
        public bool TryAcquire(int entryIndex, float now, float minIntervalSeconds)
        {
            if (entryIndex < 0) throw new ArgumentOutOfRangeException(nameof(entryIndex));

            this.EnsureCapacity(entryIndex + 1);

            if (minIntervalSeconds > 0f && now - this._lastPlayTime[entryIndex] < minIntervalSeconds)
                return false;

            this._lastPlayTime[entryIndex] = now;
            return true;
        }

        /// <summary>Forgets every timestamp. For service teardown.</summary>
        public void Clear() => this.ResetTimestamps(0, this._lastPlayTime.Length);

        private void EnsureCapacity(int required)
        {
            if (required <= this._lastPlayTime.Length)
                return;

            int grown = Math.Max(required, this._lastPlayTime.Length * 2);
            int previous = this._lastPlayTime.Length;

            Array.Resize(ref this._lastPlayTime, grown);
            this.ResetTimestamps(previous, grown);
        }

        /// <remarks>
        /// Negative infinity, not zero: a brand new entry must be playable at time 0, and a zero here
        /// would silently throttle the very first play of the session.
        /// </remarks>
        private void ResetTimestamps(int from, int to)
        {
            for (int i = from; i < to; i++)
                this._lastPlayTime[i] = float.NegativeInfinity;
        }
    }
}
