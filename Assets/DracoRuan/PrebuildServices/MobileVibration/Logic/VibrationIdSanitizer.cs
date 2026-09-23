using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>
    /// Turns a vibration id into the C# member name generated for it, and proposes an ASCII id when
    /// the author typed something the charset will not take.
    /// </summary>
    /// <remarks>
    /// <para>Mirrors <c>AudioIdSanitizer</c>: the mapping is deliberately almost the identity. Casing
    /// is never touched, because re-casing <c>button_tap</c> to <c>ButtonTap</c> is the fastest way
    /// to manufacture a collision with an id that was already <c>ButtonTap</c>.</para>
    ///
    /// <para><see cref="Suggest"/> is the only place transliteration happens, and what it returns is
    /// a proposal shown to the author. Applying it silently would leave the emitted value and the
    /// member name describing different things.</para>
    /// </remarks>
    public static class VibrationIdSanitizer
    {
        /// <summary>Lower-case d with stroke, U+0111.</summary>
        private const char LowerDWithStroke = 'đ';

        /// <summary>Upper-case D with stroke, U+0110.</summary>
        private const char UpperDWithStroke = 'Đ';

        /// <remarks>
        /// Reserved words only. Contextual keywords such as <c>value</c>, <c>var</c> and
        /// <c>record</c> are legal identifiers and must not be escaped, or perfectly ordinary ids
        /// would come out looking mangled.
        /// </remarks>
        private static readonly HashSet<string> ReservedKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while",
        };

        /// <summary>Whether <paramref name="word"/> is a C# reserved keyword.</summary>
        public static bool IsReservedKeyword(string word) =>
            word != null && ReservedKeywords.Contains(word);

        /// <summary>
        /// The member name to emit for <paramref name="vibrationId"/>, or a rejection explaining why
        /// nothing can be emitted for it.
        /// </summary>
        public static VibrationIdSanitizeResult ToMemberName(string vibrationId)
        {
            if (!VibrationIdFormat.IsValid(vibrationId, out string error))
                return VibrationIdSanitizeResult.Rejected(WithSuggestion(error, vibrationId));

            string member = vibrationId.Replace('-', '_');

            // The charset allows a leading digit; C# does not.
            if (member[0] >= '0' && member[0] <= '9')
                member = "_" + member;

            // A verbatim identifier keeps the emitted value untouched, so the call site still reads
            // as the id the author chose.
            if (IsReservedKeyword(member))
                member = "@" + member;

            if (string.Equals(member, vibrationId, StringComparison.Ordinal))
                return VibrationIdSanitizeResult.Ok(member);

            return VibrationIdSanitizeResult.Adjusted(
                member, $"'{vibrationId}' cannot be a C# member name as written, so it generates as '{member}'.");
        }

        /// <summary>
        /// An ASCII id built from arbitrary text, for offering to the author. Returns an empty
        /// string when nothing usable survives.
        /// </summary>
        public static string Suggest(string rawText)
        {
            if (string.IsNullOrEmpty(rawText))
                return string.Empty;

            string decomposed = MapStrokedD(rawText).Normalize(NormalizationForm.FormD);

            StringBuilder builder = new StringBuilder(decomposed.Length);
            bool separatorPending = false;

            foreach (char character in decomposed)
            {
                // Dropping the combining marks is what turns an accented vowel into a bare one.
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (!VibrationIdFormat.IsAllowedCharacter(character))
                {
                    separatorPending = true;
                    continue;
                }

                // Emitting the separator lazily collapses a run of illegal characters into a single
                // underscore and leaves none at either end.
                if (separatorPending && builder.Length > 0)
                    builder.Append('_');

                separatorPending = false;
                builder.Append(character);
            }

            return builder.ToString();
        }

        private static string WithSuggestion(string error, string vibrationId)
        {
            string suggestion = Suggest(vibrationId);

            bool worthOffering = !string.IsNullOrEmpty(suggestion)
                                 && !string.Equals(suggestion, vibrationId, StringComparison.Ordinal)
                                 && VibrationIdFormat.IsValid(suggestion, out _);

            return worthOffering ? $"{error} Try '{suggestion}'." : error;
        }

        /// <remarks>
        /// Unicode normalisation does not decompose d-with-stroke, so without this the letter is
        /// dropped entirely and a Vietnamese id silently loses a character.
        /// </remarks>
        private static string MapStrokedD(string text)
        {
            if (text.IndexOf(LowerDWithStroke) < 0 && text.IndexOf(UpperDWithStroke) < 0)
                return text;

            StringBuilder builder = new StringBuilder(text.Length);

            foreach (char character in text)
            {
                if (character == LowerDWithStroke)
                    builder.Append('d');
                else if (character == UpperDWithStroke)
                    builder.Append('D');
                else
                    builder.Append(character);
            }

            return builder.ToString();
        }
    }
}
