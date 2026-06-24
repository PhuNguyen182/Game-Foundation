#if USE_MEMORY_PACK
using MemoryPack;
#endif

namespace DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices
{
    public class MemoryPackBinaryDataSerializer<T> : IDataSerializer<T>
    {
        public object Serialize(T data)
        {
#if USE_MEMORY_PACK
            byte[] serializedData = MemoryPackSerializer.Serialize(data);
            return serializedData;
#else
            return null;
#endif
        }

        public T Deserialize(object serializedData)
        {
#if USE_MEMORY_PACK
            if (serializedData is not byte[] convertedData)
                return default;
            
            T deserializedData = MemoryPackSerializer.Deserialize<T>(convertedData);
            return deserializedData;
#else
            return null;
#endif
        }
    }
}
