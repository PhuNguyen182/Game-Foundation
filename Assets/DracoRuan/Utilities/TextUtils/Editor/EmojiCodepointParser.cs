using System.Globalization;

namespace DracoRuan.Utilities.TextUtils.Editor
{
    public static class EmojiCodepointParser
    {
        public static bool TryParse(string input, out int codepoint)
        {
            codepoint = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            if (TryParseAsEmoji(input, out codepoint))
                return true;

            if (TryParseAsHex(input, out codepoint))
                return true;

            return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out codepoint);
        }

        private static bool TryParseAsEmoji(string input, out int codepoint)
        {
            codepoint = 0;
            if (input.Length == 0 || char.IsLetterOrDigit(input[0]))
                return false;

            codepoint = char.ConvertToUtf32(input, 0);
            return true;
        }

        private static bool TryParseAsHex(string input, out int codepoint)
        {
            codepoint = 0;
            string hex = input;

            if (hex.StartsWith("U+", true, CultureInfo.InvariantCulture))
                hex = hex.Substring(2);
            else if (hex.StartsWith("0x", true, CultureInfo.InvariantCulture))
                hex = hex.Substring(2);
            else if (!ContainsHexOnlyLetters(hex))
                return false;

            return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codepoint);
        }

        private static bool ContainsHexOnlyLetters(string hex)
        {
            foreach (char c in hex)
            {
                bool isHexLetter = c is >= 'a' and <= 'f' or >= 'A' and <= 'F';
                if (!char.IsDigit(c) && !isHexLetter)
                    return false;
            }

            return true;
        }

        public static string ToHexName(int codepoint) => codepoint.ToString("x");
    }
}
