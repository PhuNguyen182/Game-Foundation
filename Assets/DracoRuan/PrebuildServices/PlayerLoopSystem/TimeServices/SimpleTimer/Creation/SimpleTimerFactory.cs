using R3;
using System;
using DracoRuan.CoreSystems.DesignPatterns.Factory;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.SimpleTimer.Creation
{
    public class SimpleTimerFactory : BaseFactory<TimerConfig, BaseTimer>
    {
        private bool _isDisposed;
        private DisposableBag _disposableBag;
        
        public override BaseTimer Create(TimerConfig arg)
        {
            BaseTimer timer = arg.Type switch
            {
                TimerType.Countdown => new CountdownTimer(arg.Time),
                TimerType.Frequency => new FrequencyTimer((int)arg.Time),
                TimerType.Stopwatch => new StopwatchTimer(arg.Time),
                _ => null,
            };

            timer?.AddTo(ref this._disposableBag);
            return timer;
        }

        private void ReleaseManagedResources()
        {
            this._disposableBag.Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;
            
            if (disposing)
            {
                this.ReleaseManagedResources();
            }
            
            this._isDisposed = true;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SimpleTimerFactory()
        {
            Dispose(false);
        }
    }
}
