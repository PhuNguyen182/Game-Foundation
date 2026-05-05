using System;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData
{
    public class SaveDataEvent : IDisposable
    {
        private bool _isDisposed;
        private readonly IDisposablePublisher<SaveDataMessage> _saveDataPublisher;
        public ISubscriber<SaveDataMessage> SaveDataSubscriber { get; }

        public SaveDataEvent(EventFactory eventFactory)
        {
            (this._saveDataPublisher, this.SaveDataSubscriber) = eventFactory.CreateEvent<SaveDataMessage>();
        }

        public void SendSaveDataMessage(SaveDataMessage message)
        {
            this._saveDataPublisher?.Publish(message);
        }
        
        private void ReleaseUnmanagedResources()
        {
            
        }

        private void ReleaseManagedResources()
        {
            this._saveDataPublisher?.Dispose();
        }

        protected virtual void Dispose(bool disposing)
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

        ~SaveDataEvent() => this.Dispose(false);
    }
}
