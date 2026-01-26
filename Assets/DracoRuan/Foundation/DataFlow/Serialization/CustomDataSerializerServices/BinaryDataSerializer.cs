using MemoryPack;

namespace DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices
{
    public class BinaryDataSerializer<T> : IDataSerializer<T>
    {
        public string FileExtension => ".bin";
        
        public object Serialize(T data)
        {
            byte[] serializedData = MemoryPackSerializer.Serialize(data);
            return serializedData;
        }

        public T Deserialize(object serializedData)
        {
            T deserializedData = MemoryPackSerializer.Deserialize<T>(serializedData as byte[]);
            return deserializedData;
        }
    }
}
