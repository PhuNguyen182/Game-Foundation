using System;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.MessageFilters
{
    public class LockFilter<T> : MessageHandlerFilter<T>
    {
        private readonly object _gate = new();

        public override void Handle(T message, Action<T> next)
        {
            lock (this._gate)
                next(message);
        }
    }
}
