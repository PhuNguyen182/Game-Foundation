using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DracoRuan.Foundation.DataFlow.Encryption
{
    /// <summary>
    /// Supplies the AES key used to obfuscate save payloads.
    /// </summary>
    /// <remarks>
    /// An interface so a game can source the key from somewhere less trivial than a string constant.
    /// Read the honesty note on <see cref="AesEncryptor"/> before relying on any of this.
    /// </remarks>
    public interface IEncryptionKeyProvider
    {
        /// <summary>32 bytes for AES-256.</summary>
        byte[] GetKey();
    }

    /// <summary>
    /// Default key provider: a compiled-in constant.
    /// </summary>
    /// <remarks>
    /// Replace it for anything that matters. A constant in the binary is recoverable by anyone who
    /// opens the binary, which on a client build is everyone who wants to.
    /// </remarks>
    public sealed class DefaultEncryptionKeyProvider : IEncryptionKeyProvider
    {
        private const string DefaultKeySource = "B7LfVHXL86jtc7gsdOYr2qG9iIpVNLIs";

        public byte[] GetKey() => Encoding.ASCII.GetBytes(DefaultKeySource);
    }

    /// <summary>
    /// AES-CBC encryption for save payloads.
    /// </summary>
    /// <remarks>
    /// <para><b>This is obfuscation, not security.</b> The key ships inside the application, so a
    /// determined player can always recover it and read or forge a save. It raises the effort of
    /// casual save editing and nothing more. Anything that must actually be trusted — currency,
    /// entitlements, leaderboard scores — has to be validated by a server.</para>
    ///
    /// <para><b>A fresh random IV per encryption, prepended to the ciphertext.</b> The previous
    /// implementation reused one hard-coded IV for every payload, which makes identical plaintexts
    /// produce identical ciphertexts and leaks that two saves are the same — defeating the point of
    /// CBC. The IV is not secret and is meant to travel with the ciphertext.</para>
    /// </remarks>
    public sealed class AesEncryptor
    {
        /// <summary>AES block size in bytes; also the length of the prepended IV.</summary>
        public const int IvLength = 16;

        private readonly IEncryptionKeyProvider _keyProvider;

        public AesEncryptor(IEncryptionKeyProvider keyProvider = null)
        {
            this._keyProvider = keyProvider ?? new DefaultEncryptionKeyProvider();
        }

        /// <summary>Encrypts bytes, returning <c>[IV][ciphertext]</c>.</summary>
        public byte[] Encrypt(ReadOnlySpan<byte> plain)
        {
            using Aes aes = Aes.Create();
            aes.Key = this._keyProvider.GetKey();
            aes.GenerateIV();

            using ICryptoTransform encryptor = aes.CreateEncryptor();
            using MemoryStream output = new();

            output.Write(aes.IV, 0, aes.IV.Length);

            using (CryptoStream crypto = new(output, encryptor, CryptoStreamMode.Write))
                crypto.Write(plain);

            return output.ToArray();
        }

        /// <summary>Decrypts bytes produced by <see cref="Encrypt"/>.</summary>
        public byte[] Decrypt(ReadOnlySpan<byte> cipher)
        {
            if (cipher.Length <= IvLength)
            {
                throw new ArgumentException(
                    $"Encrypted payload must be longer than the {IvLength}-byte IV prefix.", nameof(cipher));
            }

            using Aes aes = Aes.Create();
            aes.Key = this._keyProvider.GetKey();
            aes.IV = cipher.Slice(0, IvLength).ToArray();

            using ICryptoTransform decryptor = aes.CreateDecryptor();
            using MemoryStream input = new(cipher.Slice(IvLength).ToArray());
            using CryptoStream crypto = new(input, decryptor, CryptoStreamMode.Read);
            using MemoryStream output = new();

            crypto.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>Encrypts a UTF-8 string.</summary>
        public byte[] EncryptString(string plainText) => this.Encrypt(Encoding.UTF8.GetBytes(plainText));

        /// <summary>Decrypts to a UTF-8 string.</summary>
        public string DecryptString(ReadOnlySpan<byte> cipher) => Encoding.UTF8.GetString(this.Decrypt(cipher));
    }
}