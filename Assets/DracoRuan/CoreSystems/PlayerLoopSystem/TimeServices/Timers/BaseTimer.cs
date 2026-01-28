using System;
using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;
using DracoRuan.CoreSystems.PlayerLoopSystem.UpdateServices;
using UnityEngine;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.Timers
{
    public abstract class BaseTimer : ITimer, IUpdateHandler, IDisposable
    {
        private bool _disposed;
        private float _initialTime;

        public abstract bool IsFinished { get; }
        
        public bool IsRunning { get; private set; }
        public float CurrentTime { get; protected set; }
        
        public float Progress => Mathf.Clamp(this.CurrentTime / this._initialTime, 0f, 1f);

        public Action OnTimerStart { get; set; }
        public Action OnTimerStop { get; set; }
        public Action<float> OnTimerUpdate { get; set; }

        protected BaseTimer(float time) => this._initialTime = time;
        
        ~BaseTimer() => Dispose(false);
        
        public void Start()
        {
            this.CurrentTime = this._initialTime;
            if (this.IsRunning) 
                return;
            
            this.IsRunning = true;
            UpdateServiceManager.RegisterUpdateHandler(this);
            this.OnTimerStart?.Invoke();
        }

        public void Stop()
        {
            if (!this.IsRunning) 
                return;
            
            this.IsRunning = false;
            UpdateServiceManager.DeregisterUpdateHandler(this);
            this.OnTimerStop?.Invoke();
        }

        public abstract void Tick(float deltaTime);
        
        public void Resume() => this.IsRunning = true;
        
        public void Pause() => this.IsRunning = false;

        public virtual void Reset() => this.CurrentTime = this._initialTime;

        public virtual void Reset(float newTime)
        {
            this._initialTime = newTime;
            this.Reset();
        }

        // Call Dispose to ensure deregistration of the timer from the TimerManager
        // when the consumer is done with the timer or being destroyed
        public void Dispose()
        {
            this.Dispose(true);
            this.ReleaseAllCallbacks();
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this._disposed) 
                return;

            if (disposing)
                UpdateServiceManager.DeregisterUpdateHandler(this);

            this._disposed = true;
        }

        protected virtual void ReleaseAllCallbacks()
        {
            this.OnTimerStart = null;
            this.OnTimerUpdate = null;
            this.OnTimerStop = null;
        }
    }
}
