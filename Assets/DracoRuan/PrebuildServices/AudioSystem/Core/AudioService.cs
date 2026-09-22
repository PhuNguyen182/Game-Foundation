using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.PrebuildServices.AudioSystem.Core.Fading;
using DracoRuan.PrebuildServices.AudioSystem.Core.Loading;
using DracoRuan.PrebuildServices.AudioSystem.Core.Mixing;
using DracoRuan.PrebuildServices.AudioSystem.Core.Voices;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Interfaces;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DracoRuan.PrebuildServices.AudioSystem.Core
{
    /// <summary>
    /// The audio service: plays, stops, mixes and fades everything the game hears.
    /// </summary>
    /// <remarks>
    /// <para><b>It starts itself.</b> The boot pipeline only waits — it calls no initialise method,
    /// just <c>UniTask.WaitUntil(x.IsInitialized)</c> — so the constructor kicks off
    /// <see cref="InitializeAsync"/> and forgets it, the same shape the localisation and Firebase
    /// services use.</para>
    ///
    /// <para><b>One tick handler for the whole system.</b> <c>UpdateServiceManager</c> walks its list
    /// backwards using a static index that <c>DeregisterUpdateHandler</c> decrements by hand.
    /// Registering and deregistering dozens of handlers a second through that — which is what a
    /// handler per voice would do — makes <i>unrelated</i> handlers get skipped. This service
    /// registers once and walks its own voices.</para>
    ///
    /// <para><b>It never persists the mix.</b> <see cref="GetVolumeSnapshot"/> and
    /// <see cref="ApplyVolumeSnapshot"/> hand plain data to the game, whose own save system owns
    /// it.</para>
    /// </remarks>
    public sealed class AudioService : IAudioService, IAsyncInitializable, IUpdateHandler, IDisposable
    {
        private const string LogTag = AudioConstants.LogTag;

        private readonly AudioConfig _config;
        private readonly AudioCollection _collection;
        private readonly AudioDatabase _database = new AudioDatabase();
        private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();

        private readonly List<int> _slotsToStop = new List<int>();
        private readonly List<AudioFadeResult> _completionBuffer = new List<AudioFadeResult>();
        private readonly List<int> _channelSlotBuffer = new List<int>();
        private readonly Dictionary<int, int> _lastVariantIndexByEntry = new Dictionary<int, int>();

        private AudioMixerController _mixer;
        private AudioVoicePool _pool;
        private AudioFadeEngine _fadeEngine;
        private AudioClipLibrary _clipLibrary;
        private AudioFireRateGate _fireRateGate;

        private GameObject _voiceRoot;
        private Func<int, AudioVoice> _voiceBySlot;

        private float _time;
        private float _masterPitch = 1f;
        private bool _allPaused;
        private bool _isInitialized;
        private bool _isHealthy;
        private bool _isDisposed;

        public AudioService(AudioConfig config, AudioCollection collection)
        {
            this._config = config;
            this._collection = collection;

            this.InitializeAsync(this._disposeCts.Token).Forget();
        }

        #region Lifecycle

        /// <inheritdoc />
        public bool IsInitialized() => this._isInitialized;

        public bool IsReady => this._isInitialized && this._isHealthy;

        public int ActiveVoiceCount => this._pool?.ActiveCount ?? 0;

        /// <summary>The most voices live at once so far. Shown by the Audio Manager window.</summary>
        public int PeakVoiceCount => this._pool?.PeakActiveCount ?? 0;

        private async UniTask InitializeAsync(CancellationToken cancellation)
        {
            try
            {
                if (this._config == null)
                {
                    Debug.LogError($"[{LogTag}] No AudioConfig was supplied. Audio is disabled.");
                    return;
                }

                this._database.Initialize(this._collection);

                this._mixer = new AudioMixerController(this._config.Mixer, this._config.Channels);

                this._voiceRoot = new GameObject(AudioConstants.VoiceRootName);
                Object.DontDestroyOnLoad(this._voiceRoot);

                int channelCount = Mathf.Max(1, this._config.Channels?.Count ?? 0);
                this._pool = new AudioVoicePool(this._voiceRoot.transform, this._config.MaxTotalVoices, channelCount);
                this._voiceBySlot = this._pool.GetBySlot;

                this._fadeEngine = new AudioFadeEngine(this._pool.Capacity);
                this._clipLibrary = new AudioClipLibrary(this._config.ClipUnloadGraceSeconds);
                this._fireRateGate = new AudioFireRateGate(Mathf.Max(1, this._database.Count));

                this._pool.Prewarm(this.TotalReservedVoices());

                // Editor mixer values are written into the asset and survive between sessions, so
                // without this a build sounds different from the Editor it was built in.
                this._mixer.ApplyDefaults();

                await this.PreloadAsync(cancellation);

                UpdateServiceManager.RegisterUpdateHandler(this);
                this._isHealthy = true;
            }
            catch (OperationCanceledException)
            {
                // Disposed while starting up. Nothing to report.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{LogTag}] Initialisation failed, so every play will be ignored: {exception}");
            }
            finally
            {
                // Set even on failure. An unset flag hangs the whole boot pipeline for its thirty
                // second timeout and then carries on anyway, which is strictly worse than a service
                // that reports ready and no-ops with one clear error at startup.
                this._isInitialized = true;
            }
        }

        private async UniTask PreloadAsync(CancellationToken cancellation)
        {
            for (int index = 0; index < this._database.Count; index++)
            {
                AudioEntry entry = this._database.GetEntry(index);
                if (entry == null || !entry.Preload)
                    continue;

                try
                {
                    await this._clipLibrary
                        .PreloadAsync(entry, index, cancellation)
                        .Timeout(TimeSpan.FromSeconds(this._config.PreloadTimeoutSeconds));
                }
                catch (TimeoutException)
                {
                    // Named here so the failure is about this clip, rather than surfacing later as
                    // the boot pipeline's own timeout with nothing to go on.
                    Debug.LogError($"[{LogTag}] Preloading '{entry.Id}' took longer than "
                                   + $"{this._config.PreloadTimeoutSeconds}s. Continuing without it.");
                }
            }
        }

        private int TotalReservedVoices()
        {
            int total = 0;
            IReadOnlyList<AudioChannelRuntime> channels = this._mixer.Channels;

            for (int i = 0; i < channels.Count; i++)
                total += channels[i].ReservedVoices;

            return total;
        }

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;

            this._disposeCts.Cancel();
            UpdateServiceManager.DeregisterUpdateHandler(this);

            this._fadeEngine?.Clear();
            this._pool?.Dispose();
            this._clipLibrary?.Dispose();
            this._database.Clear();

            // Unity's overloaded == also covers the object already being destroyed on quit.
            if (this._voiceRoot != null)
                Object.Destroy(this._voiceRoot);

            this._voiceRoot = null;
            this._disposeCts.Dispose();
        }

        #endregion

        #region Tick

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // With Enter Play Mode Options the update manager's static lists are not cleared between
            // sessions, so a disposed service can still be ticked by the previous session's entry.
            if (this._isDisposed || !this._isHealthy)
                return;

            float delta = this._config.UseUnscaledTime ? Time.unscaledDeltaTime : deltaTime;
            if (delta > this._config.MaxTickDeltaSeconds)
                delta = this._config.MaxTickDeltaSeconds;

            this._time += delta;

            this.ApplyFadeCompletions(this._fadeEngine.Tick(delta, this._voiceBySlot));
            this.ReclaimFinishedVoices();
            this.SyncFollowTargets();
            this._clipLibrary.TickSweep(this._time);
        }

        /// <remarks>
        /// Copied out of the engine's buffer first: applying a completion can start another sound,
        /// which can append to that same buffer.
        /// </remarks>
        private void ApplyFadeCompletions(IReadOnlyList<AudioFadeResult> completions)
        {
            if (completions.Count == 0)
                return;

            this._completionBuffer.Clear();
            for (int i = 0; i < completions.Count; i++)
                this._completionBuffer.Add(completions[i]);

            for (int i = 0; i < this._completionBuffer.Count; i++)
            {
                AudioFadeResult result = this._completionBuffer[i];
                AudioVoice voice = this._pool.GetBySlot(result.Slot);

                if (voice == null || voice.State == AudioPlaybackState.Free)
                    continue;

                if (result.Completion == AudioFadeCompletion.Stop)
                {
                    this.ReleaseVoice(result.Slot);
                    continue;
                }

                if (result.Completion == AudioFadeCompletion.Pause)
                    this.PauseVoice(voice);
            }
        }

        private void ReclaimFinishedVoices()
        {
            IReadOnlyList<int> active = this._pool.ActiveSlots;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                int slot = active[i];
                AudioVoice voice = this._pool.GetBySlot(slot);

                // Loading has nothing to finish yet, Paused is frozen, and FadingOut is released by
                // its own fade completion.
                if (voice == null || voice.State != AudioPlaybackState.Playing || voice.IsLoop)
                    continue;

                if (this._time >= voice.ExpectedEndTime)
                    this.ReleaseVoice(slot);
            }
        }

        private void SyncFollowTargets()
        {
            IReadOnlyList<int> active = this._pool.ActiveSlots;

            for (int i = 0; i < active.Count; i++)
            {
                AudioVoice voice = this._pool.GetBySlot(active[i]);
                if (voice?.FollowTarget != null)
                    voice.Transform.position = voice.FollowTarget.position;
            }
        }

        #endregion

        #region Queries

        public IReadOnlyList<string> ChannelIds =>
            this._mixer != null ? this._mixer.ChannelIds : Array.Empty<string>();

        public bool TryGetEntry(string audioId, out AudioEntry entry) =>
            this._database.TryGetEntry(audioId, out entry);

        public bool IsPlaying(in AudioHandle handle) =>
            this._pool != null
            && this._pool.TryResolve(handle, out AudioVoice voice)
            && voice.State == AudioPlaybackState.Playing;

        public AudioPlaybackState GetState(in AudioHandle handle) =>
            this._pool != null && this._pool.TryResolve(handle, out AudioVoice voice)
                ? voice.State
                : AudioPlaybackState.Free;

        #endregion

        #region Play

        public AudioHandle Play(string audioId) => this.Play(audioId, default);

        public AudioHandle Play(string audioId, in AudioPlayRequest request)
        {
            if (!this.IsReady || !this._database.TryGetIndex(audioId, out int index))
                return AudioHandle.None;

            return this.PlayInternal(this._database.GetEntry(index), index, request, forceOneShot: false);
        }

        public AudioHandle Play(AudioEntry entry) => this.Play(entry, default);

        public AudioHandle Play(AudioEntry entry, in AudioPlayRequest request)
        {
            if (!this.IsReady || entry == null)
                return AudioHandle.None;

            int index = this._database.GetOrRegisterTransient(entry);
            return this.PlayInternal(entry, index, request, forceOneShot: false);
        }

        public AudioHandle PlayOneShot(string audioId) => this.PlayOneShot(audioId, default(AudioPlayRequest));

        public AudioHandle PlayOneShot(string audioId, Vector3 worldPosition) =>
            this.PlayOneShot(audioId, new AudioPlayRequest { Position = worldPosition });

        private AudioHandle PlayOneShot(string audioId, in AudioPlayRequest request)
        {
            if (!this.IsReady || !this._database.TryGetIndex(audioId, out int index))
                return AudioHandle.None;

            return this.PlayInternal(this._database.GetEntry(index), index, request, forceOneShot: true);
        }

        public AudioHandle PlayOneShot(AudioEntry entry, in AudioPlayRequest request)
        {
            if (!this.IsReady || entry == null)
                return AudioHandle.None;

            int index = this._database.GetOrRegisterTransient(entry);
            return this.PlayInternal(entry, index, request, forceOneShot: true);
        }

        public async UniTask<AudioHandle> PlayAsync(
            string audioId, AudioPlayRequest request = default, CancellationToken cancellation = default)
        {
            AudioHandle handle = this.Play(audioId, request);
            await this.WaitUntilAudible(handle, cancellation);
            return handle;
        }

        public async UniTask<AudioHandle> PlayAsync(
            AudioEntry entry, AudioPlayRequest request = default, CancellationToken cancellation = default)
        {
            AudioHandle handle = this.Play(entry, request);
            await this.WaitUntilAudible(handle, cancellation);
            return handle;
        }

        private async UniTask WaitUntilAudible(AudioHandle handle, CancellationToken cancellation)
        {
            if (!handle.IsValid)
                return;

            await UniTask.WaitUntil(
                () => this.GetState(handle) != AudioPlaybackState.Loading,
                cancellationToken: cancellation);
        }

        public async UniTask WaitUntilFinished(AudioHandle handle, CancellationToken cancellation = default)
        {
            if (!handle.IsValid || this._pool == null)
                return;

            await UniTask.WaitUntil(
                () => !this._pool.TryResolve(handle, out _),
                cancellationToken: cancellation);
        }

        private AudioHandle PlayInternal(
            AudioEntry entry, int entryIndex, in AudioPlayRequest request,
            bool forceOneShot, float initialFadeVolume = 1f, bool markProtected = false)
        {
            if (entry == null || entryIndex < 0)
                return AudioHandle.None;

            string channelId = request.ChannelOverride ?? entry.ChannelId;
            this._mixer.TryGetChannelIndex(channelId, out int channelIndex);
            AudioChannelRuntime channel = this._mixer.GetChannel(channelIndex);

            if (!this.PassesGate(entry, entryIndex, request))
                return AudioHandle.None;

            if (!this._pool.TryAcquire(channelIndex, channel?.MaxVoices ?? 0,
                    out AudioHandle handle, out AudioVoice voice))
            {
                if (this._config.LogDroppedPlays)
                    Debug.LogWarning($"[{LogTag}] No free voice for '{entry.Id}' "
                                     + $"({this._pool.ActiveCount}/{this._pool.Capacity} in use).");

                return AudioHandle.None;
            }

            this.ConfigureVoice(voice, entry, entryIndex, channel, request, forceOneShot, initialFadeVolume);
            voice.IsProtected = markProtected;

            this._fireRateGate.NotifyStarted(entryIndex, this._time, voice.SlotIndex);

            if (this._clipLibrary.TryGetLoaded(entry, entryIndex, out AudioClip clip))
                this.StartVoice(voice, entry, clip, request);
            else
                this.StartVoiceWhenLoaded(handle, voice, entry, entryIndex, request);

            if (request.FadeInSeconds > 0f)
                this._fadeEngine.StartFade(voice, initialFadeVolume, request.FadeInSeconds,
                    request.FadeCurve, AudioFadeCompletion.None);

            return handle;
        }

        private bool PassesGate(AudioEntry entry, int entryIndex, in AudioPlayRequest request)
        {
            if (request.IgnoreFireRate)
                return true;

            bool allowed = this._fireRateGate.TryAcquire(
                entryIndex, this._time, entry.FireRateRule, this._slotsToStop, out AudioThrottleReason reason);

            if (!allowed)
            {
                if (this._config.LogThrottledPlays)
                    Debug.Log($"[{LogTag}] '{entry.Id}' was refused: {reason}.");

                return false;
            }

            for (int i = 0; i < this._slotsToStop.Count; i++)
                this.ReleaseVoice(this._slotsToStop[i]);

            return true;
        }

        private void ConfigureVoice(
            AudioVoice voice, AudioEntry entry, int entryIndex, AudioChannelRuntime channel,
            in AudioPlayRequest request, bool forceOneShot, float initialFadeVolume)
        {
            voice.Entry = entry;
            voice.EntryIndex = entryIndex;
            voice.IsOneShot = forceOneShot;
            voice.IsLoop = !forceOneShot && (request.LoopOverride ?? entry.Loop);

            float volume = entry.ResolveVolume(Random.value) * (request.VolumeScale ?? 1f);
            float pitch = entry.ResolvePitch(Random.value) * (request.PitchScale ?? 1f);

            voice.BaseVolume = volume;
            voice.FadeVolume = initialFadeVolume;
            voice.BasePitch = pitch;
            voice.Priority = request.PriorityOverride ?? entry.Priority;
            voice.FollowTarget = request.FollowTarget;

            AudioSource source = voice.Source;
            source.loop = voice.IsLoop;
            source.priority = voice.Priority;
            source.panStereo = entry.StereoPan;
            source.outputAudioMixerGroup = channel?.MixerGroup;
            entry.Settings3D.ApplyTo(source);

            voice.ApplyVolume();
            voice.ApplyPitch(this._mixer.GetPerVoicePitch(voice.ChannelIndex), this._masterPitch);

            if (request.FollowTarget != null)
                voice.Transform.position = request.FollowTarget.position;
            else if (request.Position.HasValue)
                voice.Transform.position = request.Position.Value;
            else
                voice.Transform.localPosition = Vector3.zero;

            // A voice created while its channel is paused must arrive paused, or a sound triggered
            // from a pause menu leaks through.
            voice.PauseOnStart = this._allPaused || (channel?.Paused ?? false);
        }

        private void StartVoice(AudioVoice voice, AudioEntry entry, AudioClip clip, in AudioPlayRequest request)
        {
            AudioClip selected = clip;

            if (entry.HasVariants)
            {
                int last = this._lastVariantIndexByEntry.TryGetValue(voice.EntryIndex, out int stored) ? stored : -1;
                selected = entry.SelectClip(ref last, Random.value);
                this._lastVariantIndexByEntry[voice.EntryIndex] = last;
            }

            if (selected == null)
            {
                this.ReleaseVoice(voice.SlotIndex);
                return;
            }

            AudioSource source = voice.Source;
            source.clip = selected;
            source.time = Mathf.Clamp(request.StartTimeSeconds, 0f, Mathf.Max(0f, selected.length - 0.01f));

            if (request.DelaySeconds > 0f)
                source.PlayDelayed(request.DelaySeconds);
            else
                source.Play();

            voice.StartTime = this._time;
            voice.ExpectedEndTime = AudioVoiceLifetime.CalculateEndTime(
                this._time, request.DelaySeconds, selected.length,
                request.StartTimeSeconds, source.pitch, voice.IsLoop);

            voice.State = AudioPlaybackState.Playing;
            this._fadeEngine.NotifyVoiceStarted(voice.SlotIndex);

            if (voice.PauseOnStart)
            {
                voice.PauseOnStart = false;
                this.PauseVoice(voice);
            }
        }

        private void StartVoiceWhenLoaded(
            AudioHandle handle, AudioVoice voice, AudioEntry entry, int entryIndex, in AudioPlayRequest request)
        {
            voice.State = AudioPlaybackState.Loading;
            voice.LoadCts = CancellationTokenSource.CreateLinkedTokenSource(this._disposeCts.Token);

            AudioPlayRequest captured = request;
            this.LoadThenStartAsync(handle, entry, entryIndex, captured, voice.LoadCts.Token).Forget();
        }

        private async UniTaskVoid LoadThenStartAsync(
            AudioHandle handle, AudioEntry entry, int entryIndex,
            AudioPlayRequest request, CancellationToken cancellation)
        {
            AudioClip clip;

            try
            {
                clip = await this._clipLibrary.AcquireAsync(entry, entryIndex, cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // The slot may have been released while we waited. Resolving the handle again is the
            // second place the generation counter earns its keep.
            if (!this._pool.TryResolve(handle, out AudioVoice voice))
            {
                this._clipLibrary.Release(entryIndex);
                return;
            }

            if (clip == null)
            {
                this.ReleaseVoice(voice.SlotIndex);
                return;
            }

            this.StartVoice(voice, entry, clip, request);
        }

        #endregion

        #region Transport

        public void Stop(in AudioHandle handle, float fadeOutSeconds = 0f)
        {
            if (!this._pool.TryResolve(handle, out AudioVoice voice))
                return;

            this.StopVoice(voice, fadeOutSeconds);
        }

        public void StopChannel(string channelId, float fadeOutSeconds = 0f)
        {
            this.CollectChannelSlots(channelId);

            for (int i = 0; i < this._channelSlotBuffer.Count; i++)
            {
                AudioVoice voice = this._pool.GetBySlot(this._channelSlotBuffer[i]);
                if (voice != null && voice.State != AudioPlaybackState.Free)
                    this.StopVoice(voice, fadeOutSeconds);
            }
        }

        public void StopAll(float fadeOutSeconds = 0f)
        {
            IReadOnlyList<int> active = this._pool.ActiveSlots;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                AudioVoice voice = this._pool.GetBySlot(active[i]);
                if (voice != null && voice.State != AudioPlaybackState.Free)
                    this.StopVoice(voice, fadeOutSeconds);
            }
        }

        public void Pause(in AudioHandle handle)
        {
            if (this._pool.TryResolve(handle, out AudioVoice voice))
                this.PauseVoice(voice);
        }

        public void Resume(in AudioHandle handle)
        {
            if (this._pool.TryResolve(handle, out AudioVoice voice))
                this.ResumeVoice(voice);
        }

        public void PauseChannel(string channelId)
        {
            if (!this._mixer.TryGetChannelIndex(channelId, out int index))
                return;

            this._mixer.GetChannel(index).Paused = true;
            this.CollectChannelSlots(channelId);

            for (int i = 0; i < this._channelSlotBuffer.Count; i++)
                this.PauseVoice(this._pool.GetBySlot(this._channelSlotBuffer[i]));
        }

        public void ResumeChannel(string channelId)
        {
            if (!this._mixer.TryGetChannelIndex(channelId, out int index))
                return;

            this._mixer.GetChannel(index).Paused = false;
            this.CollectChannelSlots(channelId);

            for (int i = 0; i < this._channelSlotBuffer.Count; i++)
                this.ResumeVoice(this._pool.GetBySlot(this._channelSlotBuffer[i]));
        }

        public void PauseAll()
        {
            this._allPaused = true;
            IReadOnlyList<int> active = this._pool.ActiveSlots;

            for (int i = 0; i < active.Count; i++)
                this.PauseVoice(this._pool.GetBySlot(active[i]));
        }

        public void ResumeAll()
        {
            this._allPaused = false;
            IReadOnlyList<int> active = this._pool.ActiveSlots;

            for (int i = 0; i < active.Count; i++)
                this.ResumeVoice(this._pool.GetBySlot(active[i]));
        }

        private void StopVoice(AudioVoice voice, float fadeOutSeconds)
        {
            if (fadeOutSeconds <= 0f)
            {
                this.ReleaseVoice(voice.SlotIndex);
                return;
            }

            voice.State = AudioPlaybackState.FadingOut;
            this._fadeEngine.StartFade(voice, 0f, fadeOutSeconds,
                AudioFadeCurveType.Default, AudioFadeCompletion.Stop);
        }

        /// <remarks>
        /// Pausing a voice that is fading out is ignored, not honoured: the fade would never
        /// complete, so the voice would sit paused at some arbitrary volume forever.
        /// </remarks>
        private void PauseVoice(AudioVoice voice)
        {
            if (voice == null)
                return;

            if (voice.State == AudioPlaybackState.Loading)
            {
                voice.PauseOnStart = true;
                return;
            }

            if (voice.State != AudioPlaybackState.Playing)
                return;

            voice.Source.Pause();
            voice.PausedAtTime = this._time;
            voice.State = AudioPlaybackState.Paused;
        }

        /// <remarks>
        /// The end time is an absolute point on the service clock, so the time spent paused has to
        /// be added back. Without this a voice paused for longer than it had left is reclaimed the
        /// instant it resumes — or, worse, while it is still paused.
        /// </remarks>
        private void ResumeVoice(AudioVoice voice)
        {
            if (voice == null)
                return;

            if (voice.State == AudioPlaybackState.Loading)
            {
                voice.PauseOnStart = false;
                return;
            }

            if (voice.State != AudioPlaybackState.Paused)
                return;

            if (!float.IsPositiveInfinity(voice.ExpectedEndTime))
                voice.ExpectedEndTime += this._time - voice.PausedAtTime;

            voice.Source.UnPause();
            voice.State = AudioPlaybackState.Playing;
        }

        private void ReleaseVoice(int slot)
        {
            AudioVoice voice = this._pool.GetBySlot(slot);
            if (voice == null || voice.State == AudioPlaybackState.Free)
                return;

            int entryIndex = voice.EntryIndex;
            bool wasAddressable = voice.Entry != null && voice.Entry.ClipMode == AudioClipSourceMode.AssetReference;

            voice.LoadCts?.Cancel();
            voice.LoadCts?.Dispose();
            voice.LoadCts = null;

            this._fadeEngine.Cancel(slot);
            this._fireRateGate.NotifyStopped(entryIndex, slot);
            this._pool.Release(slot);

            if (wasAddressable)
                this._clipLibrary.Release(entryIndex);
        }

        private void CollectChannelSlots(string channelId)
        {
            this._channelSlotBuffer.Clear();

            if (!this._mixer.TryGetChannelIndex(channelId, out int channelIndex))
                return;

            IReadOnlyList<int> active = this._pool.ActiveSlots;

            for (int i = 0; i < active.Count; i++)
            {
                AudioVoice voice = this._pool.GetBySlot(active[i]);
                if (voice != null && voice.ChannelIndex == channelIndex)
                    this._channelSlotBuffer.Add(active[i]);
            }
        }

        #endregion

        #region Live voice parameters

        public void SetVoiceVolume(in AudioHandle handle, float volume01)
        {
            if (!this._pool.TryResolve(handle, out AudioVoice voice))
                return;

            voice.BaseVolume = Mathf.Clamp01(volume01);
            voice.ApplyVolume();
        }

        public void SetVoicePitch(in AudioHandle handle, float pitch)
        {
            if (!this._pool.TryResolve(handle, out AudioVoice voice))
                return;

            voice.BasePitch = pitch;
            voice.ApplyPitch(this._mixer.GetPerVoicePitch(voice.ChannelIndex), this._masterPitch);
        }

        public void SetVoicePosition(in AudioHandle handle, Vector3 worldPosition)
        {
            if (!this._pool.TryResolve(handle, out AudioVoice voice))
                return;

            voice.FollowTarget = null;
            voice.Transform.position = worldPosition;
        }

        #endregion

        #region Fades

        public void FadeTo(in AudioHandle handle, float targetVolume01, float duration,
            AudioFadeCurveType curve = AudioFadeCurveType.Default)
        {
            if (this._pool.TryResolve(handle, out AudioVoice voice))
                this._fadeEngine.StartFade(voice, Mathf.Clamp01(targetVolume01), duration,
                    curve, AudioFadeCompletion.None);
        }

        public AudioHandle CrossFadeChannel(string channelId, string toAudioId, float duration,
            AudioFadeCurveType curve = AudioFadeCurveType.EqualPower)
        {
            if (!this.IsReady || !this._database.TryGetIndex(toAudioId, out int index))
                return AudioHandle.None;

            return this.CrossFadeInternal(channelId, this._database.GetEntry(index), index, duration, curve);
        }

        public AudioHandle CrossFadeChannel(string channelId, AudioEntry toEntry, float duration,
            AudioFadeCurveType curve = AudioFadeCurveType.EqualPower)
        {
            if (!this.IsReady || toEntry == null)
                return AudioHandle.None;

            return this.CrossFadeInternal(
                channelId, toEntry, this._database.GetOrRegisterTransient(toEntry), duration, curve);
        }

        /// <remarks>
        /// <para>The incoming voice is acquired <b>before</b> anything is faded out. If it were the
        /// other way round, a full pool would leave the channel fading to silence with nothing
        /// arriving, and the caller would have no way to undo it.</para>
        ///
        /// <para>Voices already on their way out are released outright rather than faded again, so
        /// mashing an area transition cannot pile up half-faded music beds.</para>
        /// </remarks>
        private AudioHandle CrossFadeInternal(
            string channelId, AudioEntry entry, int entryIndex, float duration, AudioFadeCurveType curve)
        {
            if (!this._mixer.TryGetChannelIndex(channelId, out int channelIndex))
                return AudioHandle.None;

            this.CollectChannelSlots(channelId);

            for (int i = this._channelSlotBuffer.Count - 1; i >= 0; i--)
            {
                int slot = this._channelSlotBuffer[i];
                if (this._fadeEngine.CompletionOf(slot) == AudioFadeCompletion.Stop)
                {
                    this.ReleaseVoice(slot);
                    this._channelSlotBuffer.RemoveAt(i);
                }
            }

            // Snapshot before acquiring, so the incoming voice is not in the outgoing set.
            int[] outgoing = this._channelSlotBuffer.ToArray();

            AudioPlayRequest request = new AudioPlayRequest { ChannelOverride = channelId };
            AudioHandle handle = this.PlayInternal(
                entry, entryIndex, request, forceOneShot: false, initialFadeVolume: 0f, markProtected: true);

            if (!handle.IsValid)
            {
                Debug.LogWarning($"[{LogTag}] Could not start '{entry.Id}' for a cross-fade on "
                                 + $"'{channelId}', so nothing was changed. Raise maxTotalVoices or give "
                                 + "the channel reserved voices.");
                return AudioHandle.None;
            }

            for (int i = 0; i < outgoing.Length; i++)
            {
                AudioVoice voice = this._pool.GetBySlot(outgoing[i]);
                if (voice == null || voice.State == AudioPlaybackState.Free)
                    continue;

                voice.IsProtected = false;
                voice.State = AudioPlaybackState.FadingOut;
                this._fadeEngine.StartFade(voice, 0f, duration, curve, AudioFadeCompletion.Stop);
            }

            if (this._pool.TryResolve(handle, out AudioVoice incoming))
                this._fadeEngine.StartFade(incoming, 1f, duration, curve, AudioFadeCompletion.None);

            return handle;
        }

        #endregion

        #region Mixer

        public void SetChannelVolume(string channelId, float linear01)
        {
            if (this._mixer.TryGetChannelIndex(channelId, out int index))
                this._mixer.SetChannelVolume(index, linear01);
        }

        public float GetChannelVolume(string channelId) =>
            this._mixer.TryGetChannelIndex(channelId, out int index) ? this._mixer.GetChannelVolume(index) : 0f;

        public void SetChannelPitch(string channelId, float pitch)
        {
            if (!this._mixer.TryGetChannelIndex(channelId, out int index))
                return;

            this._mixer.SetChannelPitch(index, pitch);
            this.ReapplyPitch(index);
        }

        public float GetChannelPitch(string channelId) =>
            this._mixer.TryGetChannelIndex(channelId, out int index) ? this._mixer.GetChannelPitch(index) : 1f;

        public void SetChannelMuted(string channelId, bool muted)
        {
            if (this._mixer.TryGetChannelIndex(channelId, out int index))
                this._mixer.SetChannelMuted(index, muted);
        }

        public bool IsChannelMuted(string channelId) =>
            this._mixer.TryGetChannelIndex(channelId, out int index) && this._mixer.IsChannelMuted(index);

        public void SetMasterVolume(float linear01) =>
            this.SetChannelVolume(this._config.MasterChannelId, linear01);

        public float GetMasterVolume() => this.GetChannelVolume(this._config.MasterChannelId);

        /// <remarks>
        /// Master pitch is kept separately rather than read off the master channel. In per-voice
        /// mode a channel's pitch only reaches voices routed to that channel, and almost nothing is
        /// routed straight to master, so reusing it would make this silently do nothing.
        /// </remarks>
        public void SetMasterPitch(float pitch)
        {
            this._masterPitch = pitch;
            this.ReapplyPitch(channelIndex: -1);
        }

        private void ReapplyPitch(int channelIndex)
        {
            IReadOnlyList<int> active = this._pool.ActiveSlots;

            for (int i = 0; i < active.Count; i++)
            {
                AudioVoice voice = this._pool.GetBySlot(active[i]);
                if (voice == null || (channelIndex >= 0 && voice.ChannelIndex != channelIndex))
                    continue;

                voice.ApplyPitch(this._mixer.GetPerVoicePitch(voice.ChannelIndex), this._masterPitch);
            }
        }

        public AudioVolumeSnapshot GetVolumeSnapshot() => this._mixer.GetSnapshot();

        public void ApplyVolumeSnapshot(in AudioVolumeSnapshot snapshot) => this._mixer.ApplySnapshot(snapshot);

        #endregion
    }
}
