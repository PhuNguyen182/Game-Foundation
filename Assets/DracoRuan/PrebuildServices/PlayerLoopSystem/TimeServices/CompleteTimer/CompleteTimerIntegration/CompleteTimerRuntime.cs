using System;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;
using UnityEngine;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.CompleteTimerIntegration
{
    /// <summary>
    /// Wires the pure-C# timer core into the Unity runtime: drives its per-frame tick, re-anchors
    /// the clock on focus regain, and forwards its warnings to the project's logger.
    /// </summary>
    /// <remarks>
    /// This is the only place in the whole feature that touches <c>UnityEngine</c> or global
    /// <c>Debug</c> - everything under <c>CompleteTimer/</c> proper stays <c>noEngineReferences</c> so
    /// it can be unit tested with a fake clock (see REWRITE_PLAN.md section 4).
    /// </remarks>
    public sealed class CompleteTimerRuntime : IStartable, IDisposable
    {
        private readonly TimerClock _clock;
        private readonly TimerScheduler _scheduler;
        private readonly CompleteTimerDataController _dataController;

        private bool _isStarted;

        public CompleteTimerRuntime(TimerClock clock, TimerScheduler scheduler, CompleteTimerDataController dataController)
        {
            this._clock = clock;
            this._scheduler = scheduler;
            this._dataController = dataController;
        }

        public void Start()
        {
            UpdateServiceManager.RegisterUpdateHandler(this._scheduler);

            Application.focusChanged += this.OnApplicationFocusChanged;

            this._clock.OnAnomaly += this.OnClockAnomaly;
            this._scheduler.OnWarning += this.OnSchedulerWarning;
            this._dataController.OnWarning += this.OnSchedulerWarning;

            this._isStarted = true;
        }

        public void Dispose()
        {
            if (!this._isStarted)
                return;

            UpdateServiceManager.DeregisterUpdateHandler(this._scheduler);

            Application.focusChanged -= this.OnApplicationFocusChanged;

            this._clock.OnAnomaly -= this.OnClockAnomaly;
            this._scheduler.OnWarning -= this.OnSchedulerWarning;
            this._dataController.OnWarning -= this.OnSchedulerWarning;

            this._isStarted = false;
        }

        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                this._clock.ReanchorFromDevice();
        }

        private void OnClockAnomaly(ClockAnomaly anomaly) =>
            Debug.LogWarning($"[CompleteTimer] Clock anomaly detected: {anomaly.Kind}, delta={anomaly.DeltaMs}ms");

        private void OnSchedulerWarning(string message) => Debug.LogWarning(message);
    }
}
