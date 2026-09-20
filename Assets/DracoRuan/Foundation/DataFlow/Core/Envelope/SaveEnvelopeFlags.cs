using System;

namespace DracoRuan.Foundation.DataFlow.Core.Envelope
{
    /// <summary>
    /// Per-file markers stored in the envelope header.
    /// </summary>
    /// <remarks>
    /// Values are persisted, so existing members must never be renumbered. Add new members using the
    /// next free bit.
    /// </remarks>
    [Flags]
    public enum SaveEnvelopeFlags : byte
    {
        None = 0,

        /// <summary>Payload is compressed. Reserved — no compression codec is wired up yet.</summary>
        Compressed = 1 << 0,

        /// <summary>Payload is encrypted. Reserved — see the serialization layer.</summary>
        Encrypted = 1 << 1,

        /// <summary>
        /// File was produced by the one-time legacy layout import rather than by a normal save,
        /// so its <c>Revision</c> is 0 and its payload has never been re-serialized by this build.
        /// </summary>
        LegacyImported = 1 << 2,

        /// <summary>
        /// File was written by the Editor tool rather than by the running game. Useful when a bug
        /// report arrives with a save that was hand-edited.
        /// </summary>
        EditorAuthored = 1 << 3
    }
}
