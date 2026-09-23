using System;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Interfaces;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using Solo.MOST_IN_ONE;
using UnityEngine;
using VContainer.Unity;

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
    /// </remarks>
    public sealed class VibrationService : IVibrationService, ITickable, IDisposable
    {
        private readonly VibrationCollection _collection;
        private readonly VibrationDatabase _database = new VibrationDatabase();
        private readonly VibrationCooldownGate _cooldownGate;

        private int _currentlyPlayingIndex = -1;
        private float _time;
        private bool _isDisposed;

        public VibrationService(VibrationCollection collection)
        {
            this._collection = collection;
            this._database.Initialize(this._collection);
            this._cooldownGate = new VibrationCooldownGate(Mathf.Max(1, this._database.Count));

            // Nothing async to wait for: the database above is a dictionary built synchronously.
            this.IsReady = true;
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public bool HapticsEnabled
        {
            get => MOST_HapticFeedback.HapticsEnabled;
            set => MOST_HapticFeedback.HapticsEnabled = value;
        }

        /// <inheritdoc />
        public bool IsPlaying => MOST_HapticFeedback.IsPlaying;

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

            this._currentlyPlayingIndex = index;
            return true;
        }

        /// <inheritdoc />
        public void Stop()
        {
            MOST_HapticFeedback.Stop();
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
        public void Prewarm() => MOST_HapticFeedback.Prewarm();

        /// <inheritdoc />
        public bool IsSupported() => MOST_HapticFeedback.IsSupported();

        /// <inheritdoc />
        public bool IsCoreHapticsSupported() => MOST_HapticFeedback.IsCoreHapticsSupported();

        /// <inheritdoc />
        public void Tick()
        {
            if (this._isDisposed)
                return;

            this._time += Time.unscaledDeltaTime;

            // A Preset play is "done" the instant Play returns; only Pattern/Curve keep IsPlaying
            // true while they run. Once the plugin reports nothing running, stop tracking the id.
            if (this._currentlyPlayingIndex >= 0 && !MOST_HapticFeedback.IsPlaying)
                this._currentlyPlayingIndex = -1;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;
            this.IsReady = false;

            MOST_HapticFeedback.Stop();
            this._database.Clear();
        }
    }
}
