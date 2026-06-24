#if USE_MESSAGE_PACK
using MessagePack;
#endif

namespace DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices
{
    public class MessagePackBinaryDataSerializer<T> : IDataSerializer<T>
    {
        public object Serialize(T data)
        {
#if USE_MESSAGE_PACK
            byte[] serializedData = MessagePackSerializer.Serialize(data);
            return serializedData;
#else
            return null;
#endif
        }

        public T Deserialize(object serializedData)
        {
#if USE_MESSAGE_PACK
            if (serializedData is not byte[] convertedData)
                return default;

            T deserializedData = MessagePackSerializer.Deserialize<T>(convertedData);
            return deserializedData;
#else
            return default;
#endif
        }
    }
}