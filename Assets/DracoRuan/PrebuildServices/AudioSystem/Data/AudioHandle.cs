using System;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// A reference to one playing sound, safe to hold for as long as you like.
    /// </summary>
    /// <remarks>
    /// <para>Voices are recycled constantly. If a handle were an <c>AudioSource</c> reference or a
    /// bare id, a caller that cached the handle of a footstep two hundred milliseconds ago would
    /// end up pausing whatever took that voice next — most likely someone else's music.</para>
    ///
    /// <para>The slot indexes the pool's flat array, and the generation is bumped every time the
    /// slot is handed out. One comparison therefore proves a handle is still about the sound it was
    /// issued for, which makes every call with a stale handle a silent no-op by construction rather
    /// than by discipline. Generation zero is never issued, so <c>default</c> is invalid forever.
    /// </para>
    /// </remarks>
    public readonly struct AudioHandle : IEquatable<AudioHandle>
    {
        /// <summary>A handle that refers to nothing. What a refused play request returns.</summary>
        public static readonly AudioHandle None = default;

        private readonly int _slot;
        private readonly int _generation;

        internal AudioHandle(int slot, int generation)
        {
            this._slot = slot;
            this._generation = generation;
        }

        /// <summary>Index into the voice pool.</summary>
        internal int Slot => this._slot;

        /// <summary>Which occupancy of <see cref="Slot"/> this handle was issued for.</summary>
        internal int Generation => this._generation;

        /// <summary>
        /// Whether this handle was ever issued. It does not mean the sound is still playing:
        /// only the service can answer that.
        /// </summary>
        public bool IsValid => this._generation != 0;

        public bool Equals(AudioHandle other) =>
            this._slot == other._slot && this._generation == other._generation;

        public override bool Equals(object obj) => obj is AudioHandle other && this.Equals(other);

        public override int GetHashCode() => (this._slot * 397) ^ this._generation;

        public static bool operator ==(AudioHandle left, AudioHandle right) => left.Equals(right);

        public static bool operator !=(AudioHandle left, AudioHandle right) => !left.Equals(right);

        public override string ToString() =>
            this.IsValid ? $"AudioHandle(slot:{this._slot} gen:{this._generation})" : "AudioHandle.None";
    }
}
