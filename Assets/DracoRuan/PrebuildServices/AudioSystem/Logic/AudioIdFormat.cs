using System;

namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// Validation for audio entry identifiers.
    /// </summary>
    /// <remarks>
    /// <para>The charset matches <c>DomainId</c>'s, but for a different reason. A save domain id is
    /// constrained because it goes into a file path; an audio id is constrained because it is
    /// emitted as a C# <c>const</c> member name. Anything outside this set either fails to compile
    /// or has to be rewritten behind the author's back, and a member name that differs from the id
    /// it stands for is how two visibly different ids end up colliding invisibly.</para>
    ///
    /// <para>Rejecting at the point of entry, rather than transliterating, is what keeps a diff of
    /// the generated identifier file readable as a plain list of ids.</para>
    /// </remarks>
    public static class AudioIdFormat
    {
        /// <summary>Longest permitted id.</summary>
        public const int MaxLength = 64;

        /// <summary>Whether <paramref name="character"/> may appear in an audio id.</summary>
        public static bool IsAllowedCharacter(char character) =>
            character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-';

        /// <summary>Returns whether <paramref name="audioId"/> is usable, and if not, why.</summary>
        public static bool IsValid(string audioId, out string error)
        {
            if (string.IsNullOrEmpty(audioId))
            {
                error = "Audio id must not be empty.";
                return false;
            }

            if (audioId.Length > MaxLength)
            {
                error = $"Audio id '{audioId}' is longer than {MaxLength} characters.";
                return false;
            }

            foreach (char character in audioId)
            {
                if (IsAllowedCharacter(character))
                    continue;

                error = $"Audio id '{audioId}' contains '{character}'. Only letters, digits, '_' and '-' " +
                        "are allowed, because the id is emitted verbatim as a C# member name.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Throws when <paramref name="audioId"/> is unusable.</summary>
        public static void Validate(string audioId, string paramName = "audioId")
        {
            if (!IsValid(audioId, out string error))
                throw new ArgumentException(error, paramName);
        }
    }
}
