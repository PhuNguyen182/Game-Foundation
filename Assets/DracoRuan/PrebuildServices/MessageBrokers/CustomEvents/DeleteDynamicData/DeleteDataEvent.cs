using MessagePipe;

namespace DracoRuan.PrebuildServices.MessageBrokers.CustomEvents.DeleteDynamicData
{
    public class DeleteDataEvent : OptimizedEvent<DeleteDataMessage>
    {
        public DeleteDataEvent(EventFactory eventFactory) : base(eventFactory)
        {
        }
        
        ~DeleteDataEvent() => this.Dispose(false);
    }
}