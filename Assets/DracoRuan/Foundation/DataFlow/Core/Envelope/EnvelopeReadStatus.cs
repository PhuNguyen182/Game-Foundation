namespace DracoRuan.Foundation.DataFlow.Core.Envelope
{
    /// <summary>
    /// Outcome of decoding a save file.
    /// </summary>
    /// <remarks>
    /// Reading returns a status instead of throwing because every one of these cases is expected on
    /// real devices — interrupted writes, full storage, sideloaded builds, hand-edited files — and
    /// the caller's response differs per case. Collapsing them into one exception would push every
    /// caller toward "treat as missing and start fresh", which silently destroys player saves.
    /// </remarks>
    public enum EnvelopeReadStatus
    {
        /// <summary>Header and payload decoded, checksum verified.</summary>
        Success = 0,

        /// <summary>No file at the requested path.</summary>
        NotFound,

        /// <summary>Fewer bytes than the frozen prefix — an empty or truncated-at-birth file.</summary>
        TooShort,

        /// <summary>Magic bytes absent. Not a DataFlow save, or the start of the file was clobbered.</summary>
        BadMagic,

        /// <summary>
        /// Written by a newer build using a header layout this build cannot parse. Distinct from
        /// <see cref="SchemaDowngrade"/>, which is about the payload.
        /// </summary>
        UnsupportedFormatVersion,

        /// <summary>Declared header length is impossible for the declared format version.</summary>
        HeaderLengthInvalid,

        /// <summary>Declared payload length disagrees with the bytes actually present.</summary>
        PayloadLengthInvalid,

        /// <summary>Bytes are intact-looking but the checksum does not match — corruption.</summary>
        ChecksumMismatch,

        /// <summary>
        /// Payload schema version is newer than this build supports. The file is well-formed; the
        /// build is simply too old. Must never be treated as up-to-date — deserializing it would
        /// drop the newer fields and the next autosave would write that loss back permanently.
        /// </summary>
        SchemaDowngrade
    }
}
