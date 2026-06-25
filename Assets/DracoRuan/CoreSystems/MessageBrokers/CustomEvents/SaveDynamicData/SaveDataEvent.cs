using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData
{
    public class SaveDataEvent : OptimizedEvent<SaveDataMessage>
    {
        public SaveDataEvent(EventFactory eventFactory) : base(eventFactory)
        {
        }
        
        ~SaveDataEvent() => this.Dispose(false);
    }
}
