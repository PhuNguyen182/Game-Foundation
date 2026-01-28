using System;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.MessageFilters
{
    public class PredicateFilter<T> : MessageHandlerFilter<T>
    {
        private readonly Func<T, bool> _predicate;

        public PredicateFilter(Func<T, bool> predicate) => _predicate = predicate;

        public override void Handle(T message, Action<T> next)
        {
            if (this._predicate(message))
                next(message);
        }
    }
}
