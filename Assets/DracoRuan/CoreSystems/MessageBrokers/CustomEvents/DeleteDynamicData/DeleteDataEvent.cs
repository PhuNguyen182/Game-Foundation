using System;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.CustomEvents.DeleteDynamicData
{
    public class DeleteDataEvent : IDisposable
    {
        private bool _isDisposed;
        private readonly IDisposablePublisher<DeleteDataMessage> _deleteDataPublisher;
        public ISubscriber<DeleteDataMessage> DeleteDataSubscriber { get; }

        public DeleteDataEvent(EventFactory eventFactory)
        {
            (this._deleteDataPublisher, this.DeleteDataSubscriber) = eventFactory.CreateEvent<DeleteDataMessage>();
        }

        public void SendSaveDataMessage(DeleteDataMessage message)
        {
            this._deleteDataPublisher?.Publish(message);
        }
        
        private void ReleaseUnmanagedResources()
        {
            
        }

        private void ReleaseManagedResources()
        {
            this._deleteDataPublisher?.Dispose();
        }

        private void Dispose(bool disposing)
        {
            if (this._isDisposed) 
                return;
            
            this.ReleaseUnmanagedResources();
            if (disposing)
            {
                this.ReleaseManagedResources();
            }
            
            this._isDisposed = true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DeleteDataEvent() => this.Dispose(false);
    }
}