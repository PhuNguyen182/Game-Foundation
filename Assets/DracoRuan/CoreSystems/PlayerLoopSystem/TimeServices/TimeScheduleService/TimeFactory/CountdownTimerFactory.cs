using DracoRuan.CoreSystems.DesignPatterns.Factory;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Data;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.TimeSchedulerComponent;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.TimeFactory
{
    public class CountdownTimerFactory : BaseFactory<TimeSchedulerConfig, CountdownTimer>
    {
        public override CountdownTimer Produce(TimeSchedulerConfig config)
        {
            CountdownTimer countdownTimer = new(config.Key, config.Duration);
            return countdownTimer;
        }

        public CountdownTimer ProduceFromSaveData(CountdownTimerData data)
        {
            CountdownTimer countdownTimer = new(data);
            return countdownTimer;
        }
    }
}
