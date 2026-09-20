using System;

namespace DracoRuan.Foundation.DataFlow.Core.Envelope
{
    /// <summary>
    /// FNV-1a 64-bit over raw bytes — the integrity checksum for persisted save files.
    /// </summary>
    /// <remarks>
    /// <para><b>These constants are part of the file format.</b> Changing the polynomial, the offset
    /// basis, or the byte order invalidates the checksum of every save file on every player's device,
    /// which would read back as corruption. They may never change; a different algorithm requires a
    /// new <c>formatVersion</c>.</para>
    ///
    /// <para><b>This detects corruption, not tampering.</b> The hash is unkeyed, so anyone who can
    /// edit the file can recompute it. A matching checksum means "these bytes are intact", never
    /// "these bytes are trustworthy". Anti-tamper would need a keyed MAC and a key the client cannot
    /// hand over, which a client-side save cannot provide.</para>
    ///
    /// <para>Deliberately duplicated rather than shared with
    /// <c>DracoRuan.Utilities.StringUtils.StringHasher</c>: that type lives in
    /// <c>Assembly-CSharp</c>, which an assembly definition cannot reference, and a format-critical
    /// constant is better owned by the format than borrowed from a string utility.</para>
    /// </remarks>
    public static class Fnv1A
    {
        private const ulong Prime64 = 1099511628211;

        /// <summary>Starting value for a hash, for callers chaining several buffers.</summary>
        public const ulong OffsetBasis64 = 14695981039346656037;

        /// <summary>Hashes a single buffer.</summary>
        public static ulong Hash(ReadOnlySpan<byte> input) => Hash(input, OffsetBasis64);

        /// <summary>
        /// Continues a hash from <paramref name="seed"/> so several buffers can be treated as one
        /// stream without allocating a combined array.
        /// </summary>
        public static ulong Hash(ReadOnlySpan<byte> input, ulong seed)
        {
            unchecked
            {
                ulong hash = seed;
                for (int i = 0; i < input.Length; i++)
                {
                    hash ^= input[i];
                    hash *= Prime64;
                }

                return hash;
            }
        }
    }
}
