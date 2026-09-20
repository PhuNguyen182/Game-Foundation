using System;
using System.Text;
using DracoRuan.Foundation.DataFlow.Encryption;
using Newtonsoft.Json;

namespace DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices
{
    /// <summary>
    /// JSON serializer that obfuscates the result with AES.
    /// </summary>
    /// <remarks>
    /// <para><b>Previously this silently destroyed data.</b> It encrypted the JSON, then kept only
    /// the first eight bytes of the ciphertext - <c>BitConverter.ToDouble(cipheredJson)</c> - and
    /// stored that number as text. Everything past those eight bytes was discarded, so any payload
    /// longer than a few characters could never be recovered. It round-tripped through the type
    /// system perfectly and lost the data in practice.</para>
    ///
    /// <para>It now encrypts the whole payload and returns Base64, which survives being stored as
    /// text. See <see cref="AesEncryptor"/> for why this is obfuscation rather than security.</para>
    /// </remarks>
    public sealed class EncryptedJsonDataSerializer<T> : IDataSerializer<T>
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.None
        };

        private readonly AesEncryptor _encryptor;

        public EncryptedJsonDataSerializer(AesEncryptor encryptor = null)
        {
            this._encryptor = encryptor ?? new AesEncryptor();
        }

        public object Serialize(T data)
        {
            string json = JsonConvert.SerializeObject(data, JsonSettings);
            byte[] encrypted = this._encryptor.Encrypt(Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(encrypted);
        }

        public T Deserialize(object serializedData)
        {
            if (serializedData is not string base64 || base64.Length == 0)
                return default;

            byte[] encrypted = Convert.FromBase64String(base64);
            byte[] plain = this._encryptor.Decrypt(encrypted);
            string json = Encoding.UTF8.GetString(plain);

            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
