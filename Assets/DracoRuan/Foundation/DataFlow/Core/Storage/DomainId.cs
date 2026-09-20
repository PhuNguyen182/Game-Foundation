using System;

namespace DracoRuan.Foundation.DataFlow.Core.Storage
{
    /// <summary>
    /// Validation for save domain identifiers.
    /// </summary>
    /// <remarks>
    /// <para><b>A domain id is permanent.</b> It names a file on every player's device, so renaming
    /// one orphans their save. This is why a domain id must be an explicit constant rather than
    /// <c>typeof(T).Name</c> — with the type name, an ordinary class rename or namespace move
    /// silently destroys everyone's progress, and nothing in the compiler or the tests would
    /// notice.</para>
    ///
    /// <para>The character rules exist because the id goes into a path and a search pattern:
    /// a separator would escape the save directory, and a wildcard would make version enumeration
    /// match unrelated files.</para>
    /// </remarks>
    public static class DomainId
    {
        /// <summary>Longest permitted id. Keeps the final path well clear of platform limits.</summary>
        public const int MaxLength = 64;

        /// <summary>
        /// Returns whether <paramref name="domainId"/> is usable, and if not, why.
        /// </summary>
        public static bool IsValid(string domainId, out string error)
        {
            if (string.IsNullOrEmpty(domainId))
            {
                error = "Domain id must not be empty.";
                return false;
            }

            if (domainId.Length > MaxLength)
            {
                error = $"Domain id '{domainId}' is longer than {MaxLength} characters.";
                return false;
            }

            foreach (char c in domainId)
            {
                bool allowed = (c >= 'a' && c <= 'z')
                               || (c >= 'A' && c <= 'Z')
                               || (c >= '0' && c <= '9')
                               || c == '_'
                               || c == '-';

                if (allowed)
                    continue;

                error = $"Domain id '{domainId}' contains '{c}'. Only letters, digits, '_' and '-' are allowed, " +
                        "because the id is used verbatim in a file path and a search pattern.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Throws when <paramref name="domainId"/> is unusable.</summary>
        public static void Validate(string domainId, string paramName = "domainId")
        {
            if (!IsValid(domainId, out string error))
                throw new ArgumentException(error, paramName);
        }
    }
}
