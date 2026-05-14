using R3;
using System;
using System.Collections.Generic;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models;
using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimerRegistry : IUpdateHandler, IDisposable
    {
        private readonly Dictionary<string, TimerCounter> _timerCounters = new();
        
        private DisposableBag _disposableBag;
        private bool _isDisposed;

        #region Timer Access

        public TimerCounter GetTimer(string timerId) => this._timerCounters.GetValueOrDefault(timerId);

        #endregion

        #region Timer Registry

        public void RegisterTimer(string timerId, long startUnixTime, long duration)
        {
            TimerModel timerModel = new TimerModel(timerId, startUnixTime, duration);
            TimerCounter timerCounter = new TimerCounter(timerModel);
            this.AddTimer(timerCounter);
        }

        public void RegisterTimer(string timerId, long startUnixTime, params long[] durations)
        {
            TimerModel timerModel = new TimerModel(timerId, startUnixTime, durations);
            TimerCounter timerCounter = new TimerCounter(timerModel);
            this.AddTimer(timerCounter);
        }

        public void RegisterTimer(string timerId, long startUnixTime, List<long> durations)
        {
            TimerModel timerModel = new TimerModel(timerId, startUnixTime, durations);
            TimerCounter timerCounter = new TimerCounter(timerModel);
            this.AddTimer(timerCounter);
        }

        #endregion

        #region Timer Collection Modifier
        
        private void AddTimer(TimerCounter timerCounter)
        {
            timerCounter.OnTimerCompleted = RemoveTimerOnCompleted;
            timerCounter.AddTo(ref _disposableBag);
            this._timerCounters.Add(timerCounter.TimerId, timerCounter);
            return;

            void RemoveTimerOnCompleted() => this.RemoveTimer(timerCounter.TimerId);
        }

        private void RemoveTimer(string timerId)
        {
            if (!this._timerCounters.TryGetValue(timerId, out TimerCounter timerCounter)) 
                return;
            
            timerCounter?.Dispose();
            this._timerCounters.Remove(timerId);
        }
        
        #endregion
        
        public void Tick(float deltaTime)
        {
            foreach (var kvp in this._timerCounters)
            {
                kvp.Value?.Tick(deltaTime);
            }
        }

        private void ReleaseUnmanagedResources()
        {
            
        }
        
        private void ReleaseManagedResources()
        {
            this._timerCounters.Clear();
            this._disposableBag.Dispose();
        }

        private void Dispose(bool disposing)
        {
            if (this._isDisposed)
                return;
            
            this.ReleaseUnmanagedResources();
            if (disposing)
            {
                this.ReleaseManagedResources();
            }

            this._isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TimerRegistry() => this.Dispose(false);
    }
}
