using System;
using System.Collections.Generic;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.MessageFilters
{
    public class ChangedValueFilter<T> : MessageHandlerFilter<T>
    {
        private T _lastValue;
        
        public override void Handle(T message, Action<T> next)
        {
            if (EqualityComparer<T>.Default.Equals(message, this._lastValue))
                return;

            this._lastValue = message;
            next(message);
        }
    }
}
