using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Core.Envelope;

namespace DracoRuan.Foundation.DataFlow.Sync
{
    /// <summary>How to resolve a local save disagreeing with the server's copy.</summary>
    public enum SyncConflictPolicy
    {
        /// <summary>Keep the device's copy.</summary>
        LocalWins = 0,

        /// <summary>Keep the server's copy.</summary>
        RemoteWins,

        /// <summary>
        /// Keep whichever has the higher <see cref="SaveEnvelopeHeader.Revision"/>. Correct only
        /// when both copies share a <see cref="SaveEnvelopeHeader.DeviceEpochId"/>: after a
        /// reinstall the local counter restarts at zero while the server's keeps climbing, so
        /// comparing revisions across epochs would silently discard the player's progress.
        /// </summary>
        HighestRevision,

        /// <summary>Surface both to the player and let them choose.</summary>
        Manual
    }

    /// <summary>Outcome of a sync operation.</summary>
    public readonly struct SyncResult
    {
        private SyncResult(bool succeeded, bool hasConflict, string error)
        {
            this.Succeeded = succeeded;
            this.HasConflict = hasConflict;
            this.Error = error;
        }

        public bool Succeeded { get; }
        public bool HasConflict { get; }
        public string Error { get; }

        public static SyncResult Ok() => new(true, false, null);
        public static SyncResult Conflict() => new(false, true, null);
        public static SyncResult Failed(string error) => new(false, false, error);
    }

    /// <summary>
    /// Moves save data between the device and a server.
    /// </summary>
    /// <remarks>
    /// <para><b>Declared but deliberately not implemented.</b> Online support is planned, not built.
    /// What matters now is that the <i>file format</i> already carries what any implementation will
    /// need — <see cref="SaveEnvelopeHeader.Revision"/>,
    /// <see cref="SaveEnvelopeHeader.LastModifiedUtcMs"/> and
    /// <see cref="SaveEnvelopeHeader.DeviceEpochId"/> are written on every save today. Adding those
    /// fields later would mean a format-version bump and a migration of every save file every player
    /// owns, purely to add bookkeeping.</para>
    ///
    /// <para>Note for whoever implements this: a device clock is user-settable and can run
    /// backwards, so <c>LastModifiedUtcMs</c> cannot order edits on its own. The revision counter
    /// is the orderable field, and it is only comparable within one <c>DeviceEpochId</c>.</para>
    /// </remarks>
    public interface ISaveSyncProvider
    {
        /// <summary>Fetches the server's copy of a domain, if any.</summary>
        UniTask<SyncResult> PullAsync(string domainId, CancellationToken cancellationToken = default);

        /// <summary>Uploads the device's copy of a domain.</summary>
        UniTask<SyncResult> PushAsync(string domainId, CancellationToken cancellationToken = default);
    }
}
