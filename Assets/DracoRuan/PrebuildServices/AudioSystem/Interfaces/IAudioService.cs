using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Interfaces
{
    /// <summary>
    /// Plays, stops and mixes the game's audio.
    /// </summary>
    /// <remarks>
    /// <para>Every play returns an <see cref="AudioHandle"/>. Holding one is safe forever: once the
    /// voice it refers to has been recycled, every call made with it becomes a silent no-op rather
    /// than affecting whatever took that voice next.</para>
    ///
    /// <para>A refused request — throttled by fire rate, or with no voice free — returns
    /// <see cref="AudioHandle.None"/> and logs nothing. Check <c>IsValid</c> if you need to know.
    /// </para>
    /// </remarks>
    public interface IAudioService
    {
        // ---- Queries ----

        /// <summary>Whether the database is indexed and the mixer configured.</summary>
        public bool IsReady { get; }

        /// <summary>Every channel declared in the config, in declaration order.</summary>
        public IReadOnlyList<string> ChannelIds { get; }

        /// <summary>Looks up an entry by its generated id.</summary>
        public bool TryGetEntry(string audioId, out AudioEntry entry);

        /// <summary>Whether <paramref name="handle"/> still refers to something audible.</summary>
        public bool IsPlaying(in AudioHandle handle);

        /// <summary>What <paramref name="handle"/> is doing, or Free if it is stale.</summary>
        public AudioPlaybackState GetState(in AudioHandle handle);

        /// <summary>How many voices are sounding right now. For diagnostics.</summary>
        public int ActiveVoiceCount { get; }

        // ---- Playback ----

        public AudioHandle Play(string audioId);

        public AudioHandle Play(string audioId, in AudioPlayRequest request);

        public AudioHandle Play(AudioEntry entry);

        public AudioHandle Play(AudioEntry entry, in AudioPlayRequest request);

        /// <summary>Plays without looping, whatever the entry says.</summary>
        public AudioHandle PlayOneShot(string audioId);

        /// <summary>Plays without looping, at a point in the world.</summary>
        public AudioHandle PlayOneShot(string audioId, Vector3 worldPosition);

        public AudioHandle PlayOneShot(AudioEntry entry, in AudioPlayRequest request);

        /// <summary>
        /// Plays, completing once the first sample is audible rather than when the voice is
        /// reserved. For cutscene synchronisation; ordinary calls should use <see cref="Play(string)"/>.
        /// </summary>
        public UniTask<AudioHandle> PlayAsync(
            string audioId, AudioPlayRequest request = default, CancellationToken cancellation = default);

        public UniTask<AudioHandle> PlayAsync(
            AudioEntry entry, AudioPlayRequest request = default, CancellationToken cancellation = default);

        /// <summary>
        /// Completes when the voice stops for any reason. Completes immediately for a stale handle.
        /// </summary>
        public UniTask WaitUntilFinished(AudioHandle handle, CancellationToken cancellation = default);

        // ---- Transport ----

        public void Stop(in AudioHandle handle, float fadeOutSeconds = 0f);

        public void StopChannel(string channelId, float fadeOutSeconds = 0f);

        public void StopAll(float fadeOutSeconds = 0f);

        /// <summary>Holds a voice at its current position. Resuming continues from there.</summary>
        public void Pause(in AudioHandle handle);

        public void Resume(in AudioHandle handle);

        /// <summary>
        /// Pauses every voice on a channel, and makes voices started while it is paused start
        /// paused, so a sound triggered from a pause menu does not leak through.
        /// </summary>
        public void PauseChannel(string channelId);

        public void ResumeChannel(string channelId);

        /// <summary>
        /// Pauses every voice this service owns. Does not touch <c>AudioListener.pause</c>, so
        /// sounds outside the service — a pause menu's own clicks — keep working.
        /// </summary>
        public void PauseAll();

        public void ResumeAll();

        // ---- Live voice parameters ----

        public void SetVoiceVolume(in AudioHandle handle, float volume01);

        public void SetVoicePitch(in AudioHandle handle, float pitch);

        public void SetVoicePosition(in AudioHandle handle, Vector3 worldPosition);

        // ---- Fades ----

        /// <summary>
        /// Fades one voice to a new volume. A fade already running on it is replaced, starting from
        /// wherever it had reached, so nothing pops.
        /// </summary>
        public void FadeTo(in AudioHandle handle, float targetVolume01, float duration,
            AudioFadeCurveType curve = AudioFadeCurveType.Default);

        /// <summary>
        /// Fades everything currently on <paramref name="channelId"/> out while fading the new entry
        /// in, over one shared duration.
        /// </summary>
        /// <remarks>
        /// Equal power by default. Two correlated tracks cross-faded on linear curves lose about
        /// 3 dB at the midpoint, which is audible as a hole in the music at every transition.
        /// </remarks>
        public AudioHandle CrossFadeChannel(string channelId, string toAudioId, float duration,
            AudioFadeCurveType curve = AudioFadeCurveType.EqualPower);

        public AudioHandle CrossFadeChannel(string channelId, AudioEntry toEntry, float duration,
            AudioFadeCurveType curve = AudioFadeCurveType.EqualPower);

        // ---- Mixer ----

        public void SetChannelVolume(string channelId, float linear01);

        public float GetChannelVolume(string channelId);

        public void SetChannelPitch(string channelId, float pitch);

        public float GetChannelPitch(string channelId);

        public void SetChannelMuted(string channelId, bool muted);

        public bool IsChannelMuted(string channelId);

        public void SetMasterVolume(float linear01);

        public float GetMasterVolume();

        public void SetMasterPitch(float pitch);

        // ---- Settings hand-off ----

        /// <summary>
        /// The current mix, as plain data for the game to save.
        /// </summary>
        /// <remarks>
        /// The service does not persist this itself: the project already owns a save system with
        /// migrations, and a second source of truth in PlayerPrefs would sit outside all of it.
        /// </remarks>
        public AudioVolumeSnapshot GetVolumeSnapshot();

        /// <summary>
        /// Applies a saved mix. Unknown channel ids are ignored and missing ones keep their
        /// configured default, so a build that added or dropped a channel still loads.
        /// </summary>
        public void ApplyVolumeSnapshot(in AudioVolumeSnapshot snapshot);
    }
}
