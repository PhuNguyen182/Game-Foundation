using System;
using System.Buffers.Binary;

namespace DracoRuan.Foundation.DataFlow.Core.Envelope
{
    /// <summary>
    /// Encodes and decodes the on-disk save file format.
    ///
    /// <para><b>Layout (format version 1), little-endian throughout:</b></para>
    /// <code>
    /// off  size  field
    ///   0     4  magic "DRSV"          ┐
    ///   4     2  formatVersion ushort  ├─ FROZEN FOREVER, see remarks
    ///   6     2  headerLength  ushort  ┘
    ///   8     4  schemaVersion int
    ///  12     8  revision      long
    ///  20     8  lastModified  long     (unix ms, UTC)
    ///  28    16  deviceEpochId Guid
    ///  44     1  flags         byte
    ///  45     3  reserved      (zero)
    ///  48     4  payloadLength int
    ///  52     8  checksum      ulong    (FNV-1a 64 over the file with this field zeroed)
    ///  60     -  payload
    /// </code>
    /// </summary>
    /// <remarks>
    /// <para><b>Why the first eight bytes are frozen.</b> They are the only thing a future reader can
    /// rely on to locate the payload of a file written by any past or future build. Everything after
    /// them may be reorganised under a new <c>formatVersion</c>; those eight bytes may not change
    /// meaning, size or order — ever.</para>
    ///
    /// <para><b>Endianness is explicit.</b> <see cref="BinaryPrimitives"/> is used rather than
    /// <c>BitConverter</c> (host-endian) or a <c>[StructLayout]</c> struct with <c>Marshal</c>
    /// (packing and alignment vary by platform), so a file written on one device always reads
    /// correctly on another.</para>
    ///
    /// <para><b>Verify before you deserialize.</b> A corrupt payload can declare an enormous
    /// collection length and drive the deserializer into an out-of-memory crash.
    /// <see cref="TryRead"/> validates the checksum first and callers must not deserialize a payload
    /// obtained any other way.</para>
    /// </remarks>
    public static class SaveEnvelopeCodec
    {
        /// <summary>Bytes whose meaning can never change, across every format version.</summary>
        public const int FrozenPrefixLength = 8;

        /// <summary>Format version this build writes.</summary>
        public const ushort CurrentFormatVersion = 1;

        /// <summary>Header length for <see cref="CurrentFormatVersion"/>.</summary>
        public const ushort CurrentHeaderLength = 60;

        /// <summary>Oldest format version this build can still read.</summary>
        public const ushort MinimumSupportedFormatVersion = 1;

        private const int MagicOffset = 0;
        private const int FormatVersionOffset = 4;
        private const int HeaderLengthOffset = 6;
        private const int SchemaVersionOffset = 8;
        private const int RevisionOffset = 12;
        private const int LastModifiedOffset = 20;
        private const int DeviceEpochIdOffset = 28;
        private const int FlagsOffset = 44;
        private const int PayloadLengthOffset = 48;
        private const int ChecksumOffset = 52;
        private const int ChecksumLength = 8;

        private static readonly byte[] Magic = { (byte)'D', (byte)'R', (byte)'S', (byte)'V' };

        /// <summary>
        /// Builds a complete save file: header followed by <paramref name="payload"/>, with the
        /// checksum computed last over everything.
        /// </summary>
        public static byte[] Write(
            int schemaVersion,
            long revision,
            long lastModifiedUtcMs,
            Guid deviceEpochId,
            SaveEnvelopeFlags flags,
            ReadOnlySpan<byte> payload)
        {
            byte[] buffer = new byte[CurrentHeaderLength + payload.Length];
            Span<byte> span = buffer;

            Magic.CopyTo(span.Slice(MagicOffset, Magic.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(FormatVersionOffset), CurrentFormatVersion);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(HeaderLengthOffset), CurrentHeaderLength);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(SchemaVersionOffset), schemaVersion);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(RevisionOffset), revision);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(LastModifiedOffset), lastModifiedUtcMs);

            if (!deviceEpochId.TryWriteBytes(span.Slice(DeviceEpochIdOffset, 16)))
                throw new InvalidOperationException("Failed to write device epoch id into the envelope header.");

            span[FlagsOffset] = (byte)flags;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(PayloadLengthOffset), payload.Length);

            // Checksum slot stays zero while hashing, then is filled in.
            payload.CopyTo(span.Slice(CurrentHeaderLength));

            ulong checksum = Fnv1A.Hash(span);
            BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(ChecksumOffset), checksum);

            return buffer;
        }

        /// <summary>
        /// Decodes the header only. Cheap enough to run for every domain on the boot path — it never
        /// touches the payload beyond confirming its declared length is plausible.
        /// </summary>
        /// <remarks>
        /// The checksum is <b>not</b> verified here, because doing so requires reading the payload.
        /// A header obtained from this method is safe to plan migrations against but must never be
        /// used to decide that a payload is intact.
        /// </remarks>
        public static EnvelopeReadStatus TryReadHeader(ReadOnlySpan<byte> raw, out SaveEnvelopeHeader header)
        {
            header = default;

            if (raw.Length < FrozenPrefixLength)
                return EnvelopeReadStatus.TooShort;

            if (!raw.Slice(MagicOffset, Magic.Length).SequenceEqual(Magic))
                return EnvelopeReadStatus.BadMagic;

            ushort formatVersion = BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(FormatVersionOffset));
            ushort headerLength = BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(HeaderLengthOffset));

            if (formatVersion < MinimumSupportedFormatVersion || formatVersion > CurrentFormatVersion)
                return EnvelopeReadStatus.UnsupportedFormatVersion;

            if (headerLength < CurrentHeaderLength || headerLength > raw.Length)
                return EnvelopeReadStatus.HeaderLengthInvalid;

            int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(raw.Slice(PayloadLengthOffset));
            if (payloadLength < 0 || headerLength + (long)payloadLength != raw.Length)
                return EnvelopeReadStatus.PayloadLengthInvalid;

            header = new SaveEnvelopeHeader(
                formatVersion,
                headerLength,
                BinaryPrimitives.ReadInt32LittleEndian(raw.Slice(SchemaVersionOffset)),
                BinaryPrimitives.ReadInt64LittleEndian(raw.Slice(RevisionOffset)),
                BinaryPrimitives.ReadInt64LittleEndian(raw.Slice(LastModifiedOffset)),
                new Guid(raw.Slice(DeviceEpochIdOffset, 16)),
                (SaveEnvelopeFlags)raw[FlagsOffset],
                payloadLength,
                BinaryPrimitives.ReadUInt64LittleEndian(raw.Slice(ChecksumOffset)));

            return EnvelopeReadStatus.Success;
        }

        /// <summary>
        /// Decodes a complete file and verifies its checksum. The returned payload is only valid
        /// when the status is <see cref="EnvelopeReadStatus.Success"/>.
        /// </summary>
        public static EnvelopeReadStatus TryRead(
            ReadOnlySpan<byte> raw,
            out SaveEnvelopeHeader header,
            out byte[] payload)
        {
            payload = null;

            EnvelopeReadStatus status = TryReadHeader(raw, out header);
            if (status != EnvelopeReadStatus.Success)
                return status;

            if (ComputeChecksum(raw, header.HeaderLength) != header.Checksum)
                return EnvelopeReadStatus.ChecksumMismatch;

            payload = raw.Slice(header.HeaderLength, header.PayloadLength).ToArray();
            return EnvelopeReadStatus.Success;
        }

        /// <summary>
        /// Hashes the file as if the checksum field were zero, without copying or mutating the input.
        /// </summary>
        private static ulong ComputeChecksum(ReadOnlySpan<byte> raw, int headerLength)
        {
            Span<byte> zeroedChecksum = stackalloc byte[ChecksumLength];
            zeroedChecksum.Clear();

            ulong hash = Fnv1A.Hash(raw.Slice(0, ChecksumOffset));
            hash = Fnv1A.Hash(zeroedChecksum, hash);

            const int afterChecksum = ChecksumOffset + ChecksumLength;
            if (afterChecksum < headerLength)
                hash = Fnv1A.Hash(raw.Slice(afterChecksum, headerLength - afterChecksum), hash);

            return Fnv1A.Hash(raw.Slice(headerLength), hash);
        }
    }
}