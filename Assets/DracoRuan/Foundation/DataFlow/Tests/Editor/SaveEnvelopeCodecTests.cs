using System;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using NUnit.Framework;

namespace DracoRuan.Foundation.DataFlow.Tests
{
    /// <summary>
    /// Locks down the on-disk save format.
    ///
    /// <para>
    /// Every player's save file is decoded by this codec, so a regression here is unrecoverable data
    /// loss rather than a crash. The format is also frozen: <see cref="GoldenBytes_LayoutIsFrozen"/>
    /// exists to fail loudly if anyone reorders or resizes a header field, because doing so silently
    /// reinterprets every existing file.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class SaveEnvelopeCodecTests
    {
        private static readonly Guid TestEpoch = new("0123456789abcdef0123456789abcdef");
        private static readonly byte[] TestPayload = { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02 };

        private static byte[] WriteSample(
            int schemaVersion = 3,
            long revision = 42,
            long lastModifiedUtcMs = 1_700_000_000_000,
            SaveEnvelopeFlags flags = SaveEnvelopeFlags.None,
            byte[] payload = null) =>
            SaveEnvelopeCodec.Write(
                schemaVersion, revision, lastModifiedUtcMs, TestEpoch, flags, payload ?? TestPayload);

        // ---------------------------------------------------------------------
        // Round trip
        // ---------------------------------------------------------------------

        [Test]
        public void RoundTrip_PreservesEveryHeaderField()
        {
            byte[] raw = WriteSample(flags: SaveEnvelopeFlags.LegacyImported);

            EnvelopeReadStatus status = SaveEnvelopeCodec.TryRead(raw, out SaveEnvelopeHeader header,
                out byte[] payload);

            Assert.That(status, Is.EqualTo(EnvelopeReadStatus.Success));
            Assert.That(header.FormatVersion, Is.EqualTo(SaveEnvelopeCodec.CurrentFormatVersion));
            Assert.That(header.HeaderLength, Is.EqualTo(SaveEnvelopeCodec.CurrentHeaderLength));
            Assert.That(header.SchemaVersion, Is.EqualTo(3));
            Assert.That(header.Revision, Is.EqualTo(42));
            Assert.That(header.LastModifiedUtcMs, Is.EqualTo(1_700_000_000_000));
            Assert.That(header.DeviceEpochId, Is.EqualTo(TestEpoch));
            Assert.That(header.Flags, Is.EqualTo(SaveEnvelopeFlags.LegacyImported));
            Assert.That(header.PayloadLength, Is.EqualTo(TestPayload.Length));
            Assert.That(payload, Is.EqualTo(TestPayload));
        }

        [Test]
        public void RoundTrip_EmptyPayloadIsValid()
        {
            byte[] raw = WriteSample(payload: Array.Empty<byte>());

            EnvelopeReadStatus status = SaveEnvelopeCodec.TryRead(raw, out SaveEnvelopeHeader header,
                out byte[] payload);

            Assert.That(status, Is.EqualTo(EnvelopeReadStatus.Success));
            Assert.That(header.PayloadLength, Is.Zero);
            Assert.That(payload, Is.Empty);
        }

        [Test]
        public void TryReadHeader_DoesNotRequireThePayloadToBeIntact()
        {
            // The boot scan reads headers for every domain without validating payloads. It must stay
            // cheap and must not reject a file whose payload is damaged - that is the planner's call.
            byte[] raw = WriteSample();
            raw[^1] ^= 0xFF;

            Assert.That(SaveEnvelopeCodec.TryReadHeader(raw, out SaveEnvelopeHeader header),
                Is.EqualTo(EnvelopeReadStatus.Success));
            Assert.That(header.SchemaVersion, Is.EqualTo(3));

            // ... but a full read still catches it.
            Assert.That(SaveEnvelopeCodec.TryRead(raw, out _, out _),
                Is.EqualTo(EnvelopeReadStatus.ChecksumMismatch));
        }

        // ---------------------------------------------------------------------
        // Corruption detection
        // ---------------------------------------------------------------------

        [Test]
        public void PayloadCorruption_IsDetected()
        {
            byte[] raw = WriteSample();
            raw[SaveEnvelopeCodec.CurrentHeaderLength] ^= 0x01;

            Assert.That(SaveEnvelopeCodec.TryRead(raw, out _, out _),
                Is.EqualTo(EnvelopeReadStatus.ChecksumMismatch));
        }

        /// <summary>
        /// The case the checksum exists for. An undetected flipped bit in the schema version sends
        /// the migration planner down the wrong chain and writes the result back to disk - silent,
        /// permanent data loss, strictly worse than a corrupt payload.
        /// </summary>
        [Test]
        public void HeaderCorruption_IsDetected_SchemaVersion()
        {
            byte[] raw = WriteSample(schemaVersion: 3);
            raw[8] ^= 0x01; // schemaVersion 3 -> 2

            Assert.That(SaveEnvelopeCodec.TryReadHeader(raw, out SaveEnvelopeHeader header),
                Is.EqualTo(EnvelopeReadStatus.Success), "header still parses, which is the danger");
            Assert.That(header.SchemaVersion, Is.EqualTo(2), "the bit flip really did change meaning");

            Assert.That(SaveEnvelopeCodec.TryRead(raw, out _, out _),
                Is.EqualTo(EnvelopeReadStatus.ChecksumMismatch), "so the checksum MUST cover the header");
        }

        [Test]
        public void HeaderCorruption_IsDetected_AcrossEveryMutableHeaderByte()
        {
            // Byte-by-byte sweep over the header, skipping the frozen prefix (covered by the magic
            // and format-version checks) and the checksum field itself.
            const int checksumOffset = 52;
            const int checksumEnd = checksumOffset + 8;

            for (int offset = SaveEnvelopeCodec.FrozenPrefixLength;
                 offset < SaveEnvelopeCodec.CurrentHeaderLength;
                 offset++)
            {
                if (offset >= checksumOffset && offset < checksumEnd)
                    continue;

                byte[] raw = WriteSample();
                raw[offset] ^= 0xFF;

                EnvelopeReadStatus status = SaveEnvelopeCodec.TryRead(raw, out _, out _);

                Assert.That(status, Is.Not.EqualTo(EnvelopeReadStatus.Success),
                    $"corruption at header offset {offset} went undetected");
            }
        }

        [Test]
        public void ChecksumFieldCorruption_IsDetected()
        {
            byte[] raw = WriteSample();
            raw[52] ^= 0xFF;

            Assert.That(SaveEnvelopeCodec.TryRead(raw, out _, out _),
                Is.EqualTo(EnvelopeReadStatus.ChecksumMismatch));
        }

        // ---------------------------------------------------------------------
        // Malformed input
        // ---------------------------------------------------------------------

        [Test]
        public void EmptyFile_IsTooShort() =>
            Assert.That(SaveEnvelopeCodec.TryReadHeader(Array.Empty<byte>(), out _),
                Is.EqualTo(EnvelopeReadStatus.TooShort));

        [Test]
        public void ShorterThanFrozenPrefix_IsTooShort() =>
            Assert.That(SaveEnvelopeCodec.TryReadHeader(new byte[SaveEnvelopeCodec.FrozenPrefixLength - 1], out _),
                Is.EqualTo(EnvelopeReadStatus.TooShort));

        [Test]
        public void AllZeroes_IsBadMagic() =>
            Assert.That(SaveEnvelopeCodec.TryReadHeader(new byte[128], out _),
                Is.EqualTo(EnvelopeReadStatus.BadMagic));

        [Test]
        public void RawMessagePackBytes_AreBadMagic()
        {
            // A legacy .data file fed to the new reader must be rejected cleanly, not misparsed -
            // this is what makes the legacy importer's "is it already converted?" check safe.
            byte[] legacyLooking = { 0x93, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

            Assert.That(SaveEnvelopeCodec.TryReadHeader(legacyLooking, out _),
                Is.EqualTo(EnvelopeReadStatus.BadMagic));
        }

        [Test]
        public void TruncatedPayload_IsPayloadLengthInvalid()
        {
            byte[] raw = WriteSample();
            byte[] truncated = new byte[raw.Length - 2];
            Array.Copy(raw, truncated, truncated.Length);

            Assert.That(SaveEnvelopeCodec.TryReadHeader(truncated, out _),
                Is.EqualTo(EnvelopeReadStatus.PayloadLengthInvalid));
        }

        [Test]
        public void HeaderOnlyWithNonZeroDeclaredPayload_IsPayloadLengthInvalid()
        {
            byte[] raw = WriteSample();
            byte[] headerOnly = new byte[SaveEnvelopeCodec.CurrentHeaderLength];
            Array.Copy(raw, headerOnly, headerOnly.Length);

            Assert.That(SaveEnvelopeCodec.TryReadHeader(headerOnly, out _),
                Is.EqualTo(EnvelopeReadStatus.PayloadLengthInvalid));
        }

        [Test]
        public void LyingPayloadLength_IsRejected()
        {
            // A hostile or corrupt file claiming a huge payload must never reach the deserializer,
            // which would try to allocate for it.
            byte[] raw = WriteSample();
            raw[48] = 0xFF;
            raw[49] = 0xFF;
            raw[50] = 0xFF;
            raw[51] = 0x7F; // int.MaxValue

            Assert.That(SaveEnvelopeCodec.TryReadHeader(raw, out _),
                Is.EqualTo(EnvelopeReadStatus.PayloadLengthInvalid));
        }

        [Test]
        public void NegativePayloadLength_IsRejected()
        {
            byte[] raw = WriteSample();
            raw[51] = 0xFF; // sign bit set

            Assert.That(SaveEnvelopeCodec.TryReadHeader(raw, out _),
                Is.EqualTo(EnvelopeReadStatus.PayloadLengthInvalid));
        }

        [Test]
        public void FutureFormatVersion_IsUnsupported()
        {
            byte[] raw = WriteSample();
            raw[4] = 0xFF;
            raw[5] = 0xFF;

            Assert.That(SaveEnvelopeCodec.TryReadHeader(raw, out _),
                Is.EqualTo(EnvelopeReadStatus.UnsupportedFormatVersion));
        }

        [Test]
        public void ImpossibleHeaderLength_IsRejected()
        {
            byte[] raw = WriteSample();
            raw[6] = 0x02;
            raw[7] = 0x00; // headerLength = 2, below the minimum

            Assert.That(SaveEnvelopeCodec.TryReadHeader(raw, out _),
                Is.EqualTo(EnvelopeReadStatus.HeaderLengthInvalid));
        }

        // ---------------------------------------------------------------------
        // Frozen layout
        // ---------------------------------------------------------------------

        /// <summary>
        /// Pins the byte layout. If this fails, someone changed the format: every save file already
        /// on a player's device will be reinterpreted under the new layout. That requires a
        /// <c>formatVersion</c> bump and a reader for the old version, not a change to this test.
        /// </summary>
        [Test]
        public void GoldenBytes_LayoutIsFrozen()
        {
            byte[] raw = SaveEnvelopeCodec.Write(
                schemaVersion: 0x01020304,
                revision: 0x1112131415161718,
                lastModifiedUtcMs: 0x2122232425262728,
                deviceEpochId: TestEpoch,
                flags: SaveEnvelopeFlags.EditorAuthored,
                payload: new byte[] { 0xAA, 0xBB });

            Assert.That(raw.Length, Is.EqualTo(SaveEnvelopeCodec.CurrentHeaderLength + 2));

            Assert.That(new[] { raw[0], raw[1], raw[2], raw[3] },
                Is.EqualTo(new byte[] { (byte)'D', (byte)'R', (byte)'S', (byte)'V' }), "magic");
            Assert.That(new[] { raw[4], raw[5] }, Is.EqualTo(new byte[] { 0x01, 0x00 }), "formatVersion LE");
            Assert.That(new[] { raw[6], raw[7] }, Is.EqualTo(new byte[] { 0x3C, 0x00 }), "headerLength 60 LE");
            Assert.That(new[] { raw[8], raw[9], raw[10], raw[11] },
                Is.EqualTo(new byte[] { 0x04, 0x03, 0x02, 0x01 }), "schemaVersion LE");
            Assert.That(raw[12], Is.EqualTo(0x18), "revision LE, first byte");
            Assert.That(raw[20], Is.EqualTo(0x28), "lastModified LE, first byte");
            Assert.That(raw[44], Is.EqualTo((byte)SaveEnvelopeFlags.EditorAuthored), "flags");
            Assert.That(new[] { raw[45], raw[46], raw[47] },
                Is.EqualTo(new byte[] { 0x00, 0x00, 0x00 }), "reserved must stay zero");
            Assert.That(new[] { raw[48], raw[49], raw[50], raw[51] },
                Is.EqualTo(new byte[] { 0x02, 0x00, 0x00, 0x00 }), "payloadLength LE");
            Assert.That(new[] { raw[60], raw[61] }, Is.EqualTo(new byte[] { 0xAA, 0xBB }), "payload");
        }

        [Test]
        public void ChecksumIsStable_AcrossIdenticalWrites()
        {
            byte[] first = WriteSample();
            byte[] second = WriteSample();

            Assert.That(second, Is.EqualTo(first),
                "identical inputs must produce identical bytes, or the format is not deterministic");
        }

        [Test]
        public void DifferentPayloads_ProduceDifferentChecksums()
        {
            SaveEnvelopeCodec.TryRead(WriteSample(payload: new byte[] { 1, 2, 3 }),
                out SaveEnvelopeHeader a, out _);
            SaveEnvelopeCodec.TryRead(WriteSample(payload: new byte[] { 1, 2, 4 }),
                out SaveEnvelopeHeader b, out _);

            Assert.That(a.Checksum, Is.Not.EqualTo(b.Checksum));
        }

        [Test]
        public void LastModifiedUtc_ConvertsFromUnixMilliseconds()
        {
            byte[] raw = WriteSample(lastModifiedUtcMs: 0);
            SaveEnvelopeCodec.TryReadHeader(raw, out SaveEnvelopeHeader header);

            Assert.That(header.LastModifiedUtc, Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }
    }
}
