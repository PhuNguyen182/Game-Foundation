using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Core.Voices
{
    /// <summary>
    /// A fixed set of <see cref="AudioVoice"/> slots, handed out and taken back by handle.
    /// </summary>
    /// <remarks>
    /// <para><b>Why not the project's <c>GameObjectPool</c>.</b> That pool is keyed by
    /// <c>prefab.GetEntityId()</c> and voices have no prefab; its manager scans every pool linearly
    /// to find the one owning an instance; it is global static state that outlives the container
    /// scope; and <c>ObjectPool&lt;T&gt;</c> deliberately hides its live set, while this pool has to
    /// walk every live voice each tick. Reusing it would mean reimplementing the bookkeeping anyway.
    /// </para>
    ///
    /// <para><b>One pool, per-channel caps.</b> The budget that matters is total concurrent
    /// <c>AudioSource</c>s. A pool per channel strands voices in an idle channel while another
    /// starves; a cap gives the same protection ("music can never take more than two") without the
    /// waste.</para>
    ///
    /// <para><b>No voice stealing.</b> When nothing is free the request is refused. The trade is
    /// real: an undersized pool loses sounds with no trace, which is what <c>reservedVoices</c> and
    /// the peak counter exist to make visible.</para>
    /// </remarks>
    public sealed class AudioVoicePool : IDisposable
    {
        private readonly AudioVoice[] _voices;
        private readonly int[] _generations;
        private readonly Stack<int> _free;
        private readonly List<int> _active;
        private readonly int[] _channelActiveCount;
        private readonly Transform _root;

        private bool _isDisposed;

        public AudioVoicePool(Transform root, int capacity, int channelCount)
        {
            this._root = root;
            this._voices = new AudioVoice[Mathf.Max(1, capacity)];
            this._generations = new int[this._voices.Length];
            this._free = new Stack<int>(this._voices.Length);
            this._active = new List<int>(this._voices.Length);
            this._channelActiveCount = new int[Mathf.Max(1, channelCount)];

            // Highest index first, so the first voices handed out are the low-numbered ones and the
            // hierarchy reads in order while you are watching it.
            for (int slot = this._voices.Length - 1; slot >= 0; slot--)
            {
                this._generations[slot] = 1;
                this._free.Push(slot);
            }
        }

        /// <summary>Slots currently in use. Walked by the tick; do not mutate.</summary>
        public IReadOnlyList<int> ActiveSlots => this._active;

        public int ActiveCount => this._active.Count;

        public int Capacity => this._voices.Length;

        /// <summary>The most voices ever live at once. Shown by the editor tool.</summary>
        public int PeakActiveCount { get; private set; }

        /// <summary>Creates <paramref name="count"/> voices up front so the first play does not.</summary>
        public void Prewarm(int count)
        {
            int target = Mathf.Min(count, this._voices.Length);

            for (int slot = 0; slot < target; slot++)
                this.EnsureVoice(slot);
        }

        /// <summary>
        /// Takes a free slot for <paramref name="channelIndex"/>.
        /// </summary>
        /// <param name="channelMaxVoices">The channel's cap, or 0 for none.</param>
        public bool TryAcquire(int channelIndex, int channelMaxVoices, out AudioHandle handle, out AudioVoice voice)
        {
            handle = AudioHandle.None;
            voice = null;

            if (this._isDisposed || this._free.Count == 0)
                return false;

            if (channelMaxVoices > 0
                && channelIndex >= 0
                && channelIndex < this._channelActiveCount.Length
                && this._channelActiveCount[channelIndex] >= channelMaxVoices)
                return false;

            int slot = this._free.Pop();
            voice = this.EnsureVoice(slot);
            voice.ResetForReuse();
            voice.ChannelIndex = channelIndex;
            voice.GameObject.SetActive(true);

            this._active.Add(slot);

            if (channelIndex >= 0 && channelIndex < this._channelActiveCount.Length)
                this._channelActiveCount[channelIndex]++;

            if (this._active.Count > this.PeakActiveCount)
                this.PeakActiveCount = this._active.Count;

            handle = new AudioHandle(slot, this._generations[slot]);
            return true;
        }

        /// <summary>
        /// The voice <paramref name="handle"/> refers to, if it is still that voice.
        /// </summary>
        /// <remarks>
        /// This one comparison is what makes every call with a stale handle a no-op. Every mutating
        /// API goes through it, so staleness is handled by construction rather than by remembering
        /// to check.
        /// </remarks>
        public bool TryResolve(in AudioHandle handle, out AudioVoice voice)
        {
            voice = null;

            if (!handle.IsValid || this._isDisposed)
                return false;

            int slot = handle.Slot;
            if (slot < 0 || slot >= this._voices.Length)
                return false;

            if (this._generations[slot] != handle.Generation)
                return false;

            AudioVoice candidate = this._voices[slot];
            if (candidate == null || candidate.State == AudioPlaybackState.Free)
                return false;

            voice = candidate;
            return true;
        }

        /// <summary>The voice in <paramref name="slot"/>, live or not.</summary>
        public AudioVoice GetBySlot(int slot) => this._voices[slot];

        /// <summary>A handle for a slot the caller already holds, for internal bookkeeping.</summary>
        public AudioHandle HandleFor(int slot) => new AudioHandle(slot, this._generations[slot]);

        /// <summary>
        /// Returns a slot to the pool, invalidating every handle to it.
        /// </summary>
        public void Release(int slot)
        {
            if (slot < 0 || slot >= this._voices.Length)
                return;

            AudioVoice voice = this._voices[slot];
            if (voice == null || voice.State == AudioPlaybackState.Free)
                return;

            int channelIndex = voice.ChannelIndex;
            if (channelIndex >= 0 && channelIndex < this._channelActiveCount.Length)
                this._channelActiveCount[channelIndex] = Mathf.Max(0, this._channelActiveCount[channelIndex] - 1);

            voice.ResetForReuse();
            this._active.Remove(slot);

            // Bumping on release, not on acquire, means a handle dies the moment its sound does
            // rather than when the slot is next handed out.
            this._generations[slot] = NextGeneration(this._generations[slot]);
            this._free.Push(slot);
        }

        /// <summary>Releases every live voice.</summary>
        public void ReleaseAll()
        {
            for (int i = this._active.Count - 1; i >= 0; i--)
                this.Release(this._active[i]);
        }

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this.ReleaseAll();
            this._isDisposed = true;

            for (int slot = 0; slot < this._voices.Length; slot++)
            {
                AudioVoice voice = this._voices[slot];
                if (voice?.GameObject != null)
                    UnityEngine.Object.Destroy(voice.GameObject);

                this._voices[slot] = null;
            }
        }

        private AudioVoice EnsureVoice(int slot) =>
            this._voices[slot] ??= new AudioVoice(slot, this._root);

        /// <remarks>Skips 0 on wrap, so <c>default(AudioHandle)</c> can never become valid.</remarks>
        private static int NextGeneration(int generation)
        {
            int next = generation + 1;
            return next == 0 ? 1 : next;
        }
    }
}
