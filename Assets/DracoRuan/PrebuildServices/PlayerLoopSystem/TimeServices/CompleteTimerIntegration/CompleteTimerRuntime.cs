using System;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;
using UnityEngine;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.CompleteTimerIntegration
{
    /// <summary>
    /// Wires the pure-C# timer core into the Unity runtime: drives its per-frame tick, checks clock
    /// drift and re-anchors on focus regain, and forwards its warnings to the project's logger.
    /// </summary>
    /// <remarks>
    /// This is the only place in the whole feature that touches <c>UnityEngine</c> or global
    /// <c>Debug</c> - everything under <c>CompleteTimer/</c> proper stays <c>noEngineReferences</c> so
    /// it can be unit tested with a fake clock (see REWRITE_PLAN.md section 4).
    /// </remarks>
    public sealed class CompleteTimerRuntime : IStartable, IDisposable, IUpdateHandler
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
            // Registered separately from the scheduler: this only drives TimerClock.CheckDrift(),
            // which must run every frame even if ReanchorFromDevice's focus-regain event never fires
            // (deep sleep without an OS focus transition is exactly the case REWRITE_PLAN.md 5.1
            // calls out as needing this second layer of defense).
            //
            // UpdateServiceManager.UpdateTime() ticks its handler list back-to-front, so whichever
            // handler is registered *last* runs *first* next frame. The scheduler is registered
            // first here so this runtime's drift check - registered second - ticks before it, and
            // any re-anchor it triggers is already applied by the time the scheduler reads "now".
            UpdateServiceManager.RegisterUpdateHandler(this._scheduler);
            UpdateServiceManager.RegisterUpdateHandler(this);

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

            UpdateServiceManager.DeregisterUpdateHandler(this);
            UpdateServiceManager.DeregisterUpdateHandler(this._scheduler);

            Application.focusChanged -= this.OnApplicationFocusChanged;

            this._clock.OnAnomaly -= this.OnClockAnomaly;
            this._scheduler.OnWarning -= this.OnSchedulerWarning;
            this._dataController.OnWarning -= this.OnSchedulerWarning;

            this._isStarted = false;
        }

        /// <summary>Runs before the scheduler's own tick each frame; see the ordering note in <see cref="Start"/>.</summary>
        void IUpdateHandler.Tick(float deltaTime) => this._clock.CheckDrift();

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
