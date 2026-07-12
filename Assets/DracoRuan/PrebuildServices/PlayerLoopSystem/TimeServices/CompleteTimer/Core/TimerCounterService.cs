using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimerCounterService
    {
        private readonly TimerOrchestrator _timerOrchestrator;

        public TimerCounterService(TimeValidator timeValidator)
        {
            TimerRegistry timerRegistry = new();
            UpdateServiceManager.RegisterUpdateHandler(timerRegistry);
            TimerDataController timerDataController = new TimerDataController(timeValidator);
            this._timerOrchestrator = new TimerOrchestrator(timeValidator, timerRegistry, timerDataController);
        }

        public TimerCounterUnit RegisterTimer(string timerId, DateTime startTime, float durationInSeconds)
        {
            TimerCounterUnit timerCounterUnit =
                this._timerOrchestrator.RegisterTimer(timerId, startTime, durationInSeconds);
            return timerCounterUnit;
        }

        public TimerCounterUnit RegisterTimer(string timerId, DateTime startTime, List<float> durationsInSeconds)
        {
            TimerCounterUnit timerCounterUnit =
                this._timerOrchestrator.RegisterTimer(timerId, startTime, durationsInSeconds);
            return timerCounterUnit;
        }

        public void DeregisterTimer(string timerId) => this._timerOrchestrator.DeregisterTimer(timerId);

        public void SaveTimerData() => this._timerOrchestrator.SaveAllTimerCounters();
    }
}