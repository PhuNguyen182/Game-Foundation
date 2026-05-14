using System;
using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimerCounter : IUpdateHandler, IDisposable
    {
        private readonly TimerModel _timerModel;
        private bool _isActive;
        
        public Action OnTimerStarted;
        public Action<long> OnTimerUpdate;
        public Action OnTimerCompleted;
        
        public string TimerId => this._timerModel?.TimerId;
        
        public TimerCounter(TimerModel timerModel)
        {
            this._isActive = true;
            this._timerModel = timerModel;
        }
        
        public void Tick(float deltaTime)
        {
            if (!this._isActive)
                return;
        }

        public void Dispose()
        {
            this._isActive = false;
            this.OnTimerStarted = null;
            this.OnTimerUpdate = null;
            this.OnTimerCompleted = null;
        }
    }
}
