using R3;
using System;
using System.Collections.Generic;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models;
using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimerRegistry : IUpdateHandler, IDisposable
    {
        private readonly TimeValidator _timeValidator = new();
        private readonly Dictionary<string, TimerCounterUnit> _timerCounters = new();

        private DisposableBag _disposableBag;
        private bool _isDisposed;

        #region Timer Access

        public Dictionary<string, TimerCounterUnit> CurrentTimerCounters => this._timerCounters;
        
        public TimerCounterUnit GetTimerCounterUnit(string timerId) => this._timerCounters.GetValueOrDefault(timerId);
        public bool IsTimerCounterExist(string timerId) => this._timerCounters.ContainsKey(timerId);

        #endregion

        #region Timer Registry

        public void RegisterTimer(string timerId, long startUnixTime, long duration)
        {
            TimerModel timerModel = new TimerModel(timerId, startUnixTime, duration);
            TimerCounterUnit timerCounterUnit = new TimerCounterUnit(timerModel, this._timeValidator);
            this.RegisterTimer(timerCounterUnit);
        }

        public void RegisterTimer(string timerId, long startUnixTime, List<long> durations)
        {
            TimerModel timerModel = new TimerModel(timerId, startUnixTime, durations);
            TimerCounterUnit timerCounterUnit = new TimerCounterUnit(timerModel, this._timeValidator);
            this.RegisterTimer(timerCounterUnit);
        }

        public void RegisterTimer(TimerCounterUnit timerCounterUnit) => this.AddTimerCounter(timerCounterUnit);

        #endregion

        #region Timer Collection Modifier

        private void AddTimerCounter(TimerCounterUnit timerCounterUnit)
        {
            timerCounterUnit.AddTo(ref _disposableBag);
            this._timerCounters.Add(timerCounterUnit.TimerId, timerCounterUnit);
        }

        public void DeregisterTimer(string timerId)
        {
            if (!this._timerCounters.TryGetValue(timerId, out TimerCounterUnit timerCounter))
                return;

            timerCounter?.Dispose();
            this._timerCounters.Remove(timerId);
        }

        #endregion

        public void Tick(float deltaTime)
        {
            if (this._timerCounters.Count <= 0)
                return;
            
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
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TimerRegistry() => this.Dispose(false);
    }
}
