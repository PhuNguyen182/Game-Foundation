using System;
using System.Collections.Generic;
using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimerCounterUnit : IUpdateHandler, IDisposable
    {
        private readonly TimerModel _timerModel;
        private readonly TimeValidator _timeValidator;

        private bool _isFirstTickCounterPlay;
        private bool _isActive;

        public Action<int> OnTimerTierChanged;
        public Action<long> OnTimerUpdate;
        public Action OnTimerCompleted;
        public Action OnTimerRemoved;

        public TimerModel TimerModel => this._timerModel;
        public bool IsTimerCompleted { get; private set; }
        public string TimerId => this._timerModel?.TimerId;

        public TimerCounterUnit(TimerModel timerModel, TimeValidator timeValidator)
        {
            this._isActive = false;
            this.IsTimerCompleted = false;
            this._timerModel = timerModel;
            this._timeValidator = timeValidator;
            this._isFirstTickCounterPlay = true;
        }

        private void TimerUpdate()
        {
            int countingTier = 0;
            int numberOfTimerTiers = this._timerModel.TimerTierCount;
            long currentTimestamp = this._timeValidator.CurrentUnixTimestamp();
            long startTimestamp = this._timerModel.StartUnixTime;
            long timeDifference = currentTimestamp - startTimestamp;
            long timeOffset = 0;
            bool isFirstActiveTier = true;

            for (int i = 0; i < numberOfTimerTiers; i++)
            {
                long currentTierTime = this._timerModel.TicksByTier[i];
                if (currentTierTime <= 0)
                {
                    countingTier++;
                    continue;
                }

                timeOffset = isFirstActiveTier
                    ? currentTierTime - timeDifference   // First tier: subtract total passed time
                    : currentTierTime + timeOffset;      // Next tier: add negative overflow from previous tier
                isFirstActiveTier = false;

                if (timeOffset <= 0)
                {
                    this._timerModel.TicksByTier[i] = 0;
                    this.OnTimerUpdate?.Invoke(this._timerModel.TicksByTier[i]);
                    this.OnTimerTierChanged?.Invoke(i);
                }
                else
                {
                    this._timerModel.TicksByTier[i] = timeOffset;
                    this.OnTimerUpdate?.Invoke(this._timerModel.TicksByTier[i]);
                    break;
                }
            }

            if (countingTier != numberOfTimerTiers)
                return;

            this.OnTimerCompleted?.Invoke();
            this.IsTimerCompleted = true;
        }

        public void Tick(float deltaTime)
        {
            if (!this._isActive || this.IsTimerCompleted)
                return;

            this.TimerUpdate();
        }

        public void ForceStopImmediately()
        {
            this._isActive = false;
            this.IsTimerCompleted = true;
            this.OnTimerCompleted?.Invoke();
            this.ReleaseSelf();
        }

        public void AddTimestamp(List<float> timesInSeconds)
        {
            if (this.TimerModel.TicksByTier.Count != timesInSeconds.Count)
            {
                Debug.LogError($"Please check the tier of timer counter. This timer counter has {this.TimerModel.TicksByTier.Count} tiers of counting!");
                return;
            }

            int count = timesInSeconds.Count;
            for (int i = 0; i < count; i++)
            {
                long timestamp = TimeSpan.FromSeconds(timesInSeconds[i]).Milliseconds;
                this.TimerModel.TicksByTier[i] += timestamp;
            }
        }
        
        public void Activate()
        {
            this._isActive = true;
            if (!this._isFirstTickCounterPlay) 
                return;
            
            this.TimerUpdate();
            this._isFirstTickCounterPlay = false;
        }

        public void Deactivate() => this._isActive = false;

        public void ReleaseSelf() => this.OnTimerRemoved?.Invoke();

        public void Dispose()
        {
            this._isActive = false;
            this._isFirstTickCounterPlay = false;
            this.OnTimerTierChanged = null;
            this.OnTimerUpdate = null;
            this.OnTimerCompleted = null;
            this._timerModel?.Dispose();
        }
    }
}
