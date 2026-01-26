using System.IO;
using Newtonsoft.Json;

namespace DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices
{
    /// <summary>
    /// This type of data saver using JSON to serialize and deserialize data
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class JsonDataSerializer<T> : IDataSerializer<T>
    {
        public string FileExtension => ".json";

        public object Serialize(T data)
        {
            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented,
            };

            string json = JsonConvert.SerializeObject(data, settings);
            return json;
        }

        public T Deserialize(object name)
        {
            // Use using-statement for reading and deserializing can help prevent memory leaks in case of large data
            string nameString = name as string ?? string.Empty;
            using StringReader stringReader = new(nameString);
            using JsonTextReader jsonReader = new(stringReader);
            
            JsonSerializer jsonSerializer = new();
            T data = jsonSerializer.Deserialize<T>(jsonReader);
            return data;
        }
    }
}
