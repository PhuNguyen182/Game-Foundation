using System;

namespace DracoRuan.Foundation.DataFlow.Core.Envelope
{
    /// <summary>
    /// Fixed-size metadata prefix on every save file.
    ///
    /// <para>
    /// The header is readable without touching the payload, which is what makes the boot-time
    /// migration scan cheap: the planner reads schema versions for every domain without
    /// deserializing — or even loading — a single game object.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para><b>Two independent version axes.</b> <see cref="FormatVersion"/> describes this header's
    /// own layout; <see cref="SchemaVersion"/> describes the payload's shape. Format-version handling
    /// belongs in <see cref="SaveEnvelopeCodec"/> and must never become an <c>IDataMigrator</c> —
    /// otherwise upgrades turn into a two-dimensional matrix.</para>
    ///
    /// <para><b>Sync metadata is present from day one on purpose.</b> <see cref="Revision"/>,
    /// <see cref="LastModifiedUtcMs"/> and <see cref="DeviceEpochId"/> are unused by local-only
    /// builds, but adding them later would cost a format bump on every save file every player owns.
    /// <see cref="DeviceEpochId"/> exists because device clocks are user-settable and run backwards,
    /// so a timestamp alone cannot order edits, and a bare revision counter breaks when a player
    /// reinstalls and restarts at 0 while the server sits at 500.</para>
    /// </remarks>
    public readonly struct SaveEnvelopeHeader
    {
        /// <summary>Layout version of this header. See <see cref="SaveEnvelopeCodec"/>.</summary>
        public ushort FormatVersion { get; }

        /// <summary>Byte offset at which the payload starts.</summary>
        public ushort HeaderLength { get; }

        /// <summary>Schema version of the payload — which generated data class can read it.</summary>
        public int SchemaVersion { get; }

        /// <summary>Monotonic per-domain counter, incremented on every successful save.</summary>
        public long Revision { get; }

        /// <summary>Unix milliseconds UTC at write time. Advisory only; see the remarks above.</summary>
        public long LastModifiedUtcMs { get; }

        /// <summary>Identifies the install that produced this revision, disambiguating counters.</summary>
        public Guid DeviceEpochId { get; }

        /// <summary>Per-file markers.</summary>
        public SaveEnvelopeFlags Flags { get; }

        /// <summary>Payload length in bytes, excluding this header.</summary>
        public int PayloadLength { get; }

        /// <summary>
        /// FNV-1a 64-bit over the whole file with this field zeroed — header <i>and</i> payload.
        /// </summary>
        /// <remarks>
        /// Covering the header is not optional. A flipped bit in <see cref="SchemaVersion"/> would
        /// otherwise send the migration planner down the wrong chain and write the result back to
        /// disk, which is worse than a corrupt payload because it destroys data silently.
        /// </remarks>
        public ulong Checksum { get; }

        public SaveEnvelopeHeader(
            ushort formatVersion,
            ushort headerLength,
            int schemaVersion,
            long revision,
            long lastModifiedUtcMs,
            Guid deviceEpochId,
            SaveEnvelopeFlags flags,
            int payloadLength,
            ulong checksum)
        {
            this.FormatVersion = formatVersion;
            this.HeaderLength = headerLength;
            this.SchemaVersion = schemaVersion;
            this.Revision = revision;
            this.LastModifiedUtcMs = lastModifiedUtcMs;
            this.DeviceEpochId = deviceEpochId;
            this.Flags = flags;
            this.PayloadLength = payloadLength;
            this.Checksum = checksum;
        }

        /// <summary>Last write time as a UTC <see cref="DateTime"/>.</summary>
        public DateTime LastModifiedUtc =>
            DateTimeOffset.FromUnixTimeMilliseconds(this.LastModifiedUtcMs).UtcDateTime;

        public bool HasFlag(SaveEnvelopeFlags flag) => (this.Flags & flag) == flag;

        public override string ToString() =>
            $"schema v{this.SchemaVersion}, rev {this.Revision}, {this.PayloadLength} bytes, " +
            $"{this.LastModifiedUtc:u}, flags [{this.Flags}]";
    }
}
