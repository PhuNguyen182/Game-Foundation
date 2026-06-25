using System;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.CustomEvents
{
    public abstract class OptimizedEvent<TMessage> : IDisposable
    {
        private bool _isDisposed;
        private readonly IDisposablePublisher<TMessage> _eventPublisher;

        public ISubscriber<TMessage> EventSubscriber { get; }

        protected OptimizedEvent(EventFactory eventFactory)
        {
            (this._eventPublisher, this.EventSubscriber) = eventFactory.CreateEvent<TMessage>();
        }

        public void SendMessage(TMessage message) => this._eventPublisher?.Publish(message);
        
        protected virtual void ReleaseUnmanagedResources()
        {
            
        }

        protected virtual void ReleaseManagedResources()
        {
            this._eventPublisher?.Dispose();
        }

        protected void Dispose(bool disposing)
        {
            if (this._isDisposed) 
                return;
            
            this.ReleaseUnmanagedResources();
            if (disposing)
                this.ReleaseManagedResources();
            
            this._isDisposed = true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}