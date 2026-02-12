using System;
using System.IO;
using DracoRuan.Foundation.DataFlow.Encryption;
using Newtonsoft.Json;

namespace DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices
{
    /// <summary>
    /// This type of data saver using JSON to serialize and deserialize data.
    /// Using with AES encryption to make data harder to be stolen
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EncryptedJsonDataSerializer<T> : IDataSerializer<T>
    {
        private const int SafeJsonLength = 1000;
        
        public string FileExtension => ".jsonaes";
        private static readonly JsonSerializer JsonSerializer;
        private static readonly JsonSerializerSettings JsonSerializerSettings;

        static EncryptedJsonDataSerializer()
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
            byte[] cipheredJson = AesEncryptor.Encrypt(json);
            string encryptedJson = $"{BitConverter.ToDouble(cipheredJson)}";
            return encryptedJson;
        }

        public T Deserialize(object name)
        {
            string nameString = name as string ?? string.Empty;
            double cipheredValue = double.Parse(nameString);
            byte[] cipheredArray = BitConverter.GetBytes(cipheredValue);
            string decryptedJson = AesEncryptor.Decrypt(cipheredArray);

            T data;
            if (decryptedJson.Length >= SafeJsonLength)
            {
                using StringReader stringReader = new(decryptedJson);
                using JsonTextReader jsonReader = new(stringReader);
                data = JsonSerializer.Deserialize<T>(jsonReader);
            }
            else
            {
                data = JsonConvert.DeserializeObject<T>(decryptedJson);
            }

            return data;
        }
    }
}
