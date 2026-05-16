using System;
using System.Collections.Generic;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.Extensions;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimerOrchestrator
    {
        private readonly TimeValidator _timeValidator;
        private readonly TimerRegistry _timerRegistry;
        private readonly TimerDataController _timerDataController;

        public TimerOrchestrator(TimeValidator timeValidator, TimerRegistry timerRegistry,
            TimerDataController timerDataController)
        {
            this._timeValidator = timeValidator;
            this._timerRegistry = timerRegistry;
            this._timerDataController = timerDataController;
            this.BuildTimerCounterUnitFromLoadedData();
        }

        private void BuildTimerCounterUnitFromLoadedData()
        {
            TimerSaveData timerSaveData = this._timerDataController.TimerSaveData;
            foreach (var kvp in timerSaveData.TimerSaveDataUnits)
            {
                string timerId = kvp.Key;
                TimerCounterUnit timerCounter = this._timerDataController.GetTimerCounterUnitFromData(timerId);
                this._timerRegistry.RegisterTimer(timerCounter);
            }
        }

        public TimerCounterUnit RegisterTimer(string timerId, DateTime startTime, float durationInSeconds)
        {
            TimerCounterUnit timerCounter;
            if (this.IsTimerExist(timerId))
            {
                timerCounter = this._timerRegistry.GetTimerCounterUnit(timerId);
                return timerCounter;
            }

            long startUnixTime = TimeExtensions.DateTimeToUnixMilliseconds(startTime);
            long duration = TimeSpan.FromSeconds(durationInSeconds).Milliseconds;
            TimerModel timerModel = new TimerModel(timerId, startUnixTime, duration);
            timerCounter = new TimerCounterUnit(timerModel, this._timeValidator);
            timerCounter.OnTimerRemoved += () => this.DeregisterTimer(timerId);
            return timerCounter;
        }

        public TimerCounterUnit RegisterTimer(string timerId, DateTime startTime, List<float> durationsInSeconds)
        {
            TimerCounterUnit timerCounter;
            if (this.IsTimerExist(timerId))
            {
                timerCounter = this._timerRegistry.GetTimerCounterUnit(timerId);
                return timerCounter;
            }

            long startUnixTime = TimeExtensions.DateTimeToUnixMilliseconds(startTime);
            int tierCount = durationsInSeconds.Count;
            
            List<long> durations = new List<long>();
            for (int i = 0; i < tierCount; i++)
            {
                long duration = TimeSpan.FromSeconds(durationsInSeconds[i]).Milliseconds;
                durations.Add(duration);
            }
            
            TimerModel timerModel = new TimerModel(timerId, startUnixTime, durations);
            timerCounter = new TimerCounterUnit(timerModel, this._timeValidator);
            timerCounter.OnTimerRemoved += () => this.DeregisterTimer(timerId);
            return timerCounter;
        }
        
        private bool IsTimerExist(string timerId) => this._timerRegistry.IsTimerCounterExist(timerId);
        
        public void DeregisterTimer(string timerId)
        {
            this._timerRegistry.RemoveTimer(timerId);
            this._timerDataController.RemoveTimerCounterUnitData(timerId);
        }

        public void SaveAllTimerCounters()
        {
            foreach (var kvp in this._timerRegistry.CurrentTimerCounters)
            {
                TimerCounterUnit timerCounter = kvp.Value;
                this._timerDataController.AddOrUpdateTimerCounterUnitData(timerCounter, false);
            }
            
            this._timerDataController.SaveTimerData();
        }
    }
}
