using MessagePack;

namespace DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices
{
    public class MessagePackBinaryDataSerializer<T> : IDataSerializer<T>
    {
        public object Serialize(T data)
        {
            byte[] serializedData = MessagePackSerializer.Serialize(data);
            return serializedData;
        }

        public T Deserialize(object serializedData)
        {
            if (serializedData is not byte[] convertedData)
                return default;
            
            T deserializedData = MessagePackSerializer.Deserialize<T>(convertedData);
            return deserializedData;
        }
    }
}