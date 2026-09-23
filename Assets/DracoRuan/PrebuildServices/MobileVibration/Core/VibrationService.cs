using System;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Interfaces;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEngine;
using VContainer.Unity;
#if USE_MOST_HAPTICS
using Solo.MOST_IN_ONE;
#endif

namespace DracoRuan.PrebuildServices.MobileVibration.Core
{
    /// <summary>
    /// The vibration service: plays and stops mobile haptics through <c>MOST_HapticFeedback</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Synchronous startup, no <c>IAsyncInitializable</c>.</b> Unlike <c>AudioService</c>,
    /// which preloads clips and builds a mixer and therefore has real async work to do,
    /// <see cref="VibrationDatabase.Initialize"/> only builds a dictionary and returns immediately, so
    /// the constructor finishes ready. <c>MobileNotificationManager</c> is the project's other
    /// synchronous-startup service and does not implement <c>IAsyncInitializable</c> either.</para>
    ///
    /// <para><b>One playback slot, not a pool.</b> <c>MOST_HapticFeedback</c> tracks exactly one
    /// active playback (<c>_activePlayback</c>); there is nothing here to pool or mix, unlike
    /// <c>AudioService</c>'s voices. A second <see cref="Play"/> simply replaces whatever is running.
    /// </para>
    ///
    /// <para><b>Cooldown is reimplemented, not delegated to the plugin.</b> The plugin's own
    /// <c>GenerateWithCooldown</c>/<c>GenerateCurveWithCooldown</c> share one static
    /// <c>_lastHapticTime</c> across the whole plugin, so two different entries would throttle each
    /// other. <see cref="VibrationCooldownGate"/> keys the cooldown per entry instead, and this
    /// service only ever calls the plain, cooldown-less <c>Generate(...)</c> overloads.</para>
    ///
    /// <para><b>Everything native is gated by <c>USE_MOST_HAPTICS</c>.</b> The MOST Haptics plugin
    /// is a separate Asset Store import, not something this system can require via UPM. Without the
    /// define, every call below is a no-op and every capability query reports false, so a project
    /// that has not installed the plugin still compiles and runs — it just never vibrates.</para>
    /// </remarks>
    public sealed class VibrationService : IVibrationService, ITickable, IDisposable
    {
        private readonly VibrationDatabase _database = new();
        private readonly VibrationCooldownGate _cooldownGate;

        private int _currentlyPlayingIndex = -1;
        private float _time;
        private bool _isDisposed;

#if !USE_MOST_HAPTICS
        private bool _hapticsEnabledFallback;
#endif

        public VibrationService(VibrationCollection collection)
        {
            this._database.Initialize(collection);
            this._cooldownGate = new VibrationCooldownGate(Mathf.Max(1, this._database.Count));

            // Nothing async to wait for: the database above is a dictionary built synchronously.
            this.IsReady = true;
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public bool HapticsEnabled
        {
#if USE_MOST_HAPTICS
            get => MOST_HapticFeedback.HapticsEnabled;
            set => MOST_HapticFeedback.HapticsEnabled = value;
#else
            get => this._hapticsEnabledFallback;
            set => this._hapticsEnabledFallback = value;
#endif
        }

        /// <inheritdoc />
        public bool IsPlaying =>
#if USE_MOST_HAPTICS
            MOST_HapticFeedback.IsPlaying;
#else
            false;
#endif

        /// <inheritdoc />
        public string CurrentlyPlayingId =>
            this._currentlyPlayingIndex >= 0 ? this._database.GetEntry(this._currentlyPlayingIndex).Id : null;

        /// <inheritdoc />
        public bool TryGetEntry(string vibrationId, out VibrationEntry entry) =>
            this._database.TryGetEntry(vibrationId, out entry);

        /// <inheritdoc />
        public bool Play(string vibrationId)
        {
            if (!this.IsReady || !this.HapticsEnabled || !this._database.TryGetIndex(vibrationId, out int index))
                return false;

            VibrationEntry entry = this._database.GetEntry(index);
            if (!this._cooldownGate.TryAcquire(index, this._time, entry.MinIntervalSeconds))
                return false;

#if USE_MOST_HAPTICS
            switch (entry.SourceMode)
            {
                case VibrationSourceMode.Preset:
                    MOST_HapticFeedback.Generate(entry.PresetType);
                    break;
                case VibrationSourceMode.CustomPattern:
                    MOST_HapticFeedback.Generate(entry.CustomPattern);
                    break;
                case VibrationSourceMode.Curve:
                    MOST_HapticFeedback.Generate(entry.Curve);
                    break;
            }
#endif

            this._currentlyPlayingIndex = index;
            return true;
        }

        /// <inheritdoc />
        public void Stop()
        {
#if USE_MOST_HAPTICS
            MOST_HapticFeedback.Stop();
#endif
            this._currentlyPlayingIndex = -1;
        }

        /// <inheritdoc />
        public bool Stop(string vibrationId)
        {
            if (this._currentlyPlayingIndex < 0)
                return false;

            if (!string.Equals(
                    this._database.GetEntry(this._currentlyPlayingIndex).Id, vibrationId, StringComparison.Ordinal))
                return false;

            this.Stop();
            return true;
        }

        /// <inheritdoc />
        public void Prewarm()
        {
#if USE_MOST_HAPTICS
            MOST_HapticFeedback.Prewarm();
#endif
        }

        /// <inheritdoc />
        public bool IsSupported() =>
#if USE_MOST_HAPTICS
            MOST_HapticFeedback.IsSupported();
#else
            false;
#endif

        /// <inheritdoc />
        public bool IsCoreHapticsSupported() =>
#if USE_MOST_HAPTICS
            MOST_HapticFeedback.IsCoreHapticsSupported();
#else
            false;
#endif

        /// <summary>
        /// Inspect vibration enable or not
        /// </summary>
        /// <returns></returns>
        public bool IsHapticsEnable() => this.HapticsEnabled;

        /// <summary>
        /// Turn On/Off vibration
        /// </summary>
        /// <param name="hapticsEnabled"></param>
        public void ToggleHaptics(bool hapticsEnabled) => this.HapticsEnabled = hapticsEnabled;

        /// <inheritdoc />
        public void Tick()
        {
            if (this._isDisposed)
                return;

            this._time += Time.unscaledDeltaTime;

#if USE_MOST_HAPTICS
            // A Preset play is "done" the instant Play returns; only Pattern/Curve keep IsPlaying
            // true while they run. Once the plugin reports nothing running, stop tracking the id.
            if (this._currentlyPlayingIndex >= 0 && !MOST_HapticFeedback.IsPlaying)
                this._currentlyPlayingIndex = -1;
#endif
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;
            this.IsReady = false;

#if USE_MOST_HAPTICS
            MOST_HapticFeedback.Stop();
#endif
            this._database.Clear();
        }
    }
}