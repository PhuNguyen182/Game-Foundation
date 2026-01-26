using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DracoRuan.Foundation.DataFlow.Encryption
{
    public static class AesEncryptor
    {
        private static readonly byte[] Key;
        private static readonly byte[] InitialVector;

        private const string DefaultInitialVectorVSource = "61KAnvXaAy9yDwN9";
        private const string DefaultKeySource = "B7LfVHXL86jtc7gsdOYr2qG9iIpVNLIs";

        static AesEncryptor()
        {
            InitialVector = Encoding.ASCII.GetBytes(DefaultInitialVectorVSource);
            Key = Encoding.ASCII.GetBytes(DefaultKeySource);
        }

        public static byte[] Encrypt(string plainText, byte[] key = null, byte[] iv = null)
        {
            using AesManaged aes = new AesManaged();
            ICryptoTransform encryptor = aes.CreateEncryptor(key ?? Key, iv ?? InitialVector);
            using MemoryStream ms = new MemoryStream();
            using CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            using (StreamWriter sw = new StreamWriter(cs))
                sw.Write(plainText);

            byte[] encrypted = ms.ToArray();
            return encrypted;
        }

        public static string Decrypt(byte[] cipherText, byte[] key = null, byte[] iv = null)
        {
            using AesManaged aes = new AesManaged();
            ICryptoTransform decryptor = aes.CreateDecryptor(key ?? Key, iv ?? InitialVector);
            using MemoryStream ms = new MemoryStream(cipherText);
            using CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using StreamReader reader = new StreamReader(cs);
            var plaintext = reader.ReadToEnd();
            return plaintext;
        }
    }
}
