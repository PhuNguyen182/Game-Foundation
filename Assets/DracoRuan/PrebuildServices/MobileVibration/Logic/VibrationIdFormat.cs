using System;

namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>
    /// Validation for vibration entry identifiers.
    /// </summary>
    /// <remarks>
    /// <para>The charset matches <c>AudioIdFormat</c>'s, for the same reason: an id is emitted as a
    /// C# <c>const</c> member name, and anything outside this set either fails to compile or has to
    /// be rewritten behind the author's back, which is how two visibly different ids end up
    /// colliding invisibly.</para>
    ///
    /// <para>Rejecting at the point of entry, rather than transliterating, is what keeps a diff of
    /// the generated identifier file readable as a plain list of ids.</para>
    /// </remarks>
    public static class VibrationIdFormat
    {
        /// <summary>Longest permitted id.</summary>
        public const int MaxLength = 64;

        /// <summary>Whether <paramref name="character"/> may appear in a vibration id.</summary>
        public static bool IsAllowedCharacter(char character) =>
            (character >= 'a' && character <= 'z')
            || (character >= 'A' && character <= 'Z')
            || (character >= '0' && character <= '9')
            || character == '_'
            || character == '-';

        /// <summary>Returns whether <paramref name="vibrationId"/> is usable, and if not, why.</summary>
        public static bool IsValid(string vibrationId, out string error)
        {
            if (string.IsNullOrEmpty(vibrationId))
            {
                error = "Vibration id must not be empty.";
                return false;
            }

            if (vibrationId.Length > MaxLength)
            {
                error = $"Vibration id '{vibrationId}' is longer than {MaxLength} characters.";
                return false;
            }

            foreach (char character in vibrationId)
            {
                if (IsAllowedCharacter(character))
                    continue;

                error = $"Vibration id '{vibrationId}' contains '{character}'. Only letters, digits, '_' " +
                        "and '-' are allowed, because the id is emitted verbatim as a C# member name.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Throws when <paramref name="vibrationId"/> is unusable.</summary>
        public static void Validate(string vibrationId, string paramName = "vibrationId")
        {
            if (!IsValid(vibrationId, out string error))
                throw new ArgumentException(error, paramName);
        }
    }
}
