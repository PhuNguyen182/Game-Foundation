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
        public string FileExtension => ".jsonaes";

        public object Serialize(T data)
        {
            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented,
            };

            string json = JsonConvert.SerializeObject(data, settings);
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

            using StringReader stringReader = new(decryptedJson);
            using JsonTextReader jsonReader = new(stringReader);
            
            JsonSerializer jsonSerializer = new();
            T data = jsonSerializer.Deserialize<T>(jsonReader);
            return data;
        }
    }
}
