using System;
using MessagePipe;

namespace DracoRuan.PrebuildServices.MessageBrokers.CustomEvents
{
    public abstract class OptimizedEvent<TMessage> : IDisposable
    {
        private bool _isDisposed;
        
        public IDisposablePublisher<TMessage> EventPublisher { get; }
        public ISubscriber<TMessage> EventSubscriber { get; }

        protected OptimizedEvent(EventFactory eventFactory)
        {
            (this.EventPublisher, this.EventSubscriber) = eventFactory.CreateEvent<TMessage>();
        }

        public void SendMessage(TMessage message) => this.EventPublisher?.Publish(message);
        
        protected virtual void ReleaseUnmanagedResources()
        {
            
        }

        protected virtual void ReleaseManagedResources()
        {
            this.EventPublisher?.Dispose();
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