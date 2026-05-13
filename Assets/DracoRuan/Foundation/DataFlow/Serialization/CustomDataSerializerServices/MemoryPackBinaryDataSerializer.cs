using MemoryPack;

namespace DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices
{
    public class MemoryPackBinaryDataSerializer<T> : IDataSerializer<T>
    {
        public object Serialize(T data)
        {
            byte[] serializedData = MemoryPackSerializer.Serialize(data);
            return serializedData;
        }

        public T Deserialize(object serializedData)
        {
            if (serializedData is not byte[] convertedData)
                return default;
            
            T deserializedData = MemoryPackSerializer.Deserialize<T>(convertedData);
            return deserializedData;
        }
    }
}
