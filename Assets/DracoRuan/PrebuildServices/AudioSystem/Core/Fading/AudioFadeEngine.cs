using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Core.Voices;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Logic;

namespace DracoRuan.PrebuildServices.AudioSystem.Core.Fading
{
    /// <summary>
    /// Drives every volume fade, one per voice slot.
    /// </summary>
    /// <remarks>
    /// <para><b>One fade per voice, replaced rather than layered.</b> A new fade starts from the
    /// voice's current fade volume, not from the previous fade's start value. That single rule is
    /// why interrupting a fade cannot pop and why two fades can never fight over one
    /// <c>AudioSource.volume</c>.</para>
    ///
    /// <para>Written as a hand-rolled engine rather than DOTween, which is available: a tween per
    /// fade allocates, couples audio to a plugin, and — the real problem — outlives a voice that
    /// gets recycled mid-fade, at which point it is animating someone else's sound.</para>
    ///
    /// <para>Completions are reported to the caller rather than acted on here, so a voice is never
    /// released in the middle of the loop that is walking the fade list.</para>
    /// </remarks>
    public sealed class AudioFadeEngine
    {
        private readonly AudioFade[] _fades;
        private readonly List<int> _activeSlots;

        public AudioFadeEngine(int capacity)
        {
            this._fades = new AudioFade[capacity];
            this._activeSlots = new List<int>(capacity);
        }

        /// <summary>Whether a fade is running on <paramref name="slot"/>.</summary>
        public bool IsFading(int slot) => this._fades[slot].IsActive;

        /// <summary>What will happen to <paramref name="slot"/> when its fade ends.</summary>
        public AudioFadeCompletion CompletionOf(int slot) => this._fades[slot].Completion;

        /// <summary>
        /// Starts a fade on <paramref name="voice"/>, replacing whatever was running on it.
        /// </summary>
        /// <param name="duration">
        /// Zero or less applies the target immediately and starts no fade, which also keeps the
        /// tick free of a divide by zero.
        /// </param>
        public void StartFade(
            AudioVoice voice, float target, float duration,
            AudioFadeCurveType curve, AudioFadeCompletion completion)
        {
            int slot = voice.SlotIndex;
            float from = voice.FadeVolume;

            if (duration <= 0f)
            {
                this.Cancel(slot);

                voice.FadeVolume = target;
                voice.ApplyVolume();

                if (completion != AudioFadeCompletion.None)
                    this._pendingCompletions.Add(new AudioFadeResult(slot, completion));

                return;
            }

            if (!this._fades[slot].IsActive)
                this._activeSlots.Add(slot);

            this._fades[slot] = new AudioFade
            {
                IsActive = true,
                From = from,
                To = target,
                Duration = duration,
                Elapsed = 0f,
                Curve = AudioFadeCurve.ResolveDirectional(curve, from, target),
                Completion = completion,
                PendingStart = voice.State == AudioPlaybackState.Loading,
            };
        }

        /// <summary>Drops the fade on <paramref name="slot"/> without touching the voice.</summary>
        public void Cancel(int slot)
        {
            if (!this._fades[slot].IsActive)
                return;

            this._fades[slot] = default;
            this._activeSlots.Remove(slot);
        }

        /// <summary>
        /// Tells a held fade that its voice has started sounding, so it may begin.
        /// </summary>
        public void NotifyVoiceStarted(int slot)
        {
            if (this._fades[slot].IsActive)
                this._fades[slot].PendingStart = false;
        }

        /// <summary>
        /// Advances every fade and returns what the caller must now do to which voices.
        /// </summary>
        /// <param name="deltaTime">Already clamped by the service.</param>
        /// <param name="voiceOf">Resolves a slot to its voice, or null if it is gone.</param>
        public IReadOnlyList<AudioFadeResult> Tick(float deltaTime, System.Func<int, AudioVoice> voiceOf)
        {
            this._pendingCompletions.Clear();

            for (int i = this._activeSlots.Count - 1; i >= 0; i--)
            {
                int slot = this._activeSlots[i];
                AudioVoice voice = voiceOf(slot);

                if (voice == null || voice.State == AudioPlaybackState.Free)
                {
                    this._fades[slot] = default;
                    this._activeSlots.RemoveAt(i);
                    continue;
                }

                // A held fade has nothing to fade yet, and a paused one resumes where it stopped.
                if (this._fades[slot].PendingStart || voice.State == AudioPlaybackState.Paused)
                    continue;

                this._fades[slot].Elapsed += deltaTime;

                float progress = this._fades[slot].Elapsed / this._fades[slot].Duration;
                bool finished = progress >= 1f;

                float shaped = AudioFadeCurve.Evaluate(this._fades[slot].Curve, progress);
                voice.FadeVolume = this._fades[slot].From
                                   + ((this._fades[slot].To - this._fades[slot].From) * shaped);
                voice.ApplyVolume();

                if (!finished)
                    continue;

                AudioFadeCompletion completion = this._fades[slot].Completion;

                voice.FadeVolume = this._fades[slot].To;
                voice.ApplyVolume();

                this._fades[slot] = default;
                this._activeSlots.RemoveAt(i);

                if (completion != AudioFadeCompletion.None)
                    this._pendingCompletions.Add(new AudioFadeResult(slot, completion));
            }

            return this._pendingCompletions;
        }

        /// <summary>Drops every fade. For teardown.</summary>
        public void Clear()
        {
            for (int i = 0; i < this._fades.Length; i++)
                this._fades[i] = default;

            this._activeSlots.Clear();
            this._pendingCompletions.Clear();
        }

        private readonly List<AudioFadeResult> _pendingCompletions = new List<AudioFadeResult>();
    }

    /// <summary>A fade that finished, and what its voice now needs.</summary>
    public readonly struct AudioFadeResult
    {
        public AudioFadeResult(int slot, AudioFadeCompletion completion)
        {
            this.Slot = slot;
            this.Completion = completion;
        }

        public int Slot { get; }
        public AudioFadeCompletion Completion { get; }
    }
}
