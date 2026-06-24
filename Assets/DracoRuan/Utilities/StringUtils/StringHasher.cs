using System.Text;

namespace DracoRuan.Utilities.StringUtils
{
    public static class StringHasher
    {
        private const string AlphabetBase36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string AlphabetBase62 = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string AlphabetFullKeyboard =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()-_=+[]{}|;:',.<>/?`~";
        
        public static int HashToInt32(string input)
        {
            unchecked
            {
                const int fnvPrime = 16777619;
                const int offsetBasis = unchecked((int)2166136261);

                int hash = offsetBasis;
                for (int i = 0; i < input.Length; i++)
                {
                    hash ^= input[i];
                    hash *= fnvPrime;
                }
                
                return hash;
            }
        }

        public static ulong HashToUInt64(string input)
        {
            unchecked
            {
                const ulong fnvPrime = 1099511628211;
                const ulong offsetBasis = 14695981039346656037;

                ulong hash = offsetBasis;
                for (int i = 0; i < input.Length; i++)
                {
                    hash ^= input[i];
                    hash *= fnvPrime;
                }

                return hash;
            }
        }
        
        public static string ToBase36(ulong value) => EncodeWithAlphabet(value, AlphabetBase36);

        public static string HashToShortCodeBase36(string input) =>
            ToBase36(HashToUInt64(input));

        public static string ToBase62(ulong value) => EncodeWithAlphabet(value, AlphabetBase62);

        public static string HashToShortCodeBase62(string input) =>
            ToBase62(HashToUInt64(input));

        public static string ToFullKeyboard(ulong value) => EncodeWithAlphabet(value, AlphabetFullKeyboard);

        public static string HashToShortCodeFullKeyboard(string input) =>
            ToFullKeyboard(HashToUInt64(input));

        // ==== Generic Encoder ====
        private static string EncodeWithAlphabet(ulong value, string alphabet)
        {
            if (value == 0) 
                return alphabet[0].ToString();

            int baseN = alphabet.Length;
            StringBuilder sb = new();
            while (value > 0)
            {
                int remainder = (int)(value % (ulong)baseN);
                sb.Insert(0, alphabet[remainder]);
                value /= (ulong)baseN;
            }

            return sb.ToString();
        }
    }
}