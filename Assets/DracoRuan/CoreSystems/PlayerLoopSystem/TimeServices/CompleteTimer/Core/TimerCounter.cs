using System;
using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimerCounter : IUpdateHandler, IDisposable
    {
        private readonly TimerModel _timerModel;
        private readonly TimeValidator _timeValidator;

        private bool _isActive;

        public Action OnTimerTierChanged;
        public Action<long> OnTimerUpdate;
        public Action OnTimerCompleted;
        public Action OnTimerRemoved;
        
        public bool IsTimerCompleted { get; private set; }
        public string TimerId => this._timerModel?.TimerId;
        
        public TimerCounter(TimerModel timerModel, TimeValidator timeValidator)
        {
            this._isActive = true;
            this.IsTimerCompleted = false;
            this._timerModel = timerModel;
            this._timeValidator = timeValidator;
        }

        private void TimerUpdate()
        {
            int zeroTier = 0;
            int tierCount = this._timerModel.TierCount;
            long currentTimestamp = this._timeValidator.CurrentUnixTimestamp();
            long startTimestamp = this._timerModel.StartUnixTime;
            long timeDifference = currentTimestamp - startTimestamp;
            long timeOffset = 0;

            for (int i = 0; i < tierCount; i++)
            {
                long currentTierTime = this._timerModel.TicksByTier[i];
                if (currentTierTime <= 0)
                {
                    zeroTier += 1;
                    continue;
                }

                timeOffset = timeOffset < 0
                    ? currentTierTime - timeDifference + timeOffset
                    : currentTierTime - timeDifference;
                if (timeOffset <= 0)
                {
                    this._timerModel.TicksByTier[i] = 0;
                    this.OnTimerUpdate?.Invoke(this._timerModel.TicksByTier[i]);
                    this.OnTimerTierChanged?.Invoke();
                }
                else
                {
                    this._timerModel.TicksByTier[i] = timeOffset;
                    this.OnTimerUpdate?.Invoke(this._timerModel.TicksByTier[i]);
                    break;
                }
            }

            if (zeroTier != tierCount) 
                return;
            
            this.OnTimerCompleted?.Invoke();
            this.IsTimerCompleted = true;
        }
        
        public void Tick(float deltaTime)
        {
            if (!this._isActive || !this.IsTimerCompleted)
                return;
            
            this.TimerUpdate();
        }
        
        public void UpdateTimerOnStart() => this.TimerUpdate();

        public void RemoveSelf() => this.OnTimerRemoved?.Invoke();
        
        public void Dispose()
        {
            this._isActive = false;
            this.OnTimerTierChanged = null;
            this.OnTimerUpdate = null;
            this.OnTimerCompleted = null;
            this._timerModel?.Dispose();
        }
    }
}
