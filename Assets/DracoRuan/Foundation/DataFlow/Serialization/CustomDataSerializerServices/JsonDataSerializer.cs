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
        private const int SafeJsonLength = 1000;
        
        public string FileExtension => ".json";
        private static readonly JsonSerializer JsonSerializer;
        private static readonly JsonSerializerSettings JsonSerializerSettings;
        
        static JsonDataSerializer()
        {
            JsonSerializer = new JsonSerializer();
            JsonSerializerSettings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
            };
        }

        public object Serialize(T data)
        {
            string json = JsonConvert.SerializeObject(data, JsonSerializerSettings);
            return json;
        }

        public T Deserialize(object name)
        {
            T data;
            string nameString = name as string ?? string.Empty;
            
            if (nameString.Length >= SafeJsonLength)
            {
                using StringReader stringReader = new(nameString);
                using JsonTextReader jsonReader = new(stringReader);
                data = JsonSerializer.Deserialize<T>(jsonReader);
            }
            else
            {
                data = JsonConvert.DeserializeObject<T>(nameString);
            }

            return data;
        }
    }
}
