namespace DracoRuan.Foundation.DataFlow.Core.Storage
{
    /// <summary>
    /// What <see cref="AtomicFileStore.Recover"/> found and did for a single logical file.
    /// </summary>
    /// <remarks>
    /// Recovery is driven entirely by which of the three physical files exist — target, temp and
    /// backup — because that is the only evidence that survives a process kill. Nothing is inferred
    /// from a journal or a flag, both of which can disagree with the filesystem.
    /// </remarks>
    public enum RecoveryOutcome
    {
        /// <summary>No files present. A domain that has never been saved.</summary>
        NothingToDo = 0,

        /// <summary>Target present and intact. The normal steady state.</summary>
        TargetIntact,

        /// <summary>
        /// A write was interrupted before the target was replaced. The stale temp file was removed;
        /// the previous target is untouched and still current.
        /// </summary>
        DiscardedStaleTemp,

        /// <summary>
        /// Target was missing or unreadable and a backup existed, so the backup was promoted. The
        /// player loses at most the most recent save rather than the whole file.
        /// </summary>
        PromotedBackup,

        /// <summary>
        /// A write was interrupted after the target was moved aside but before the temp file took
        /// its place. The temp file holds the newer, complete content, so it was promoted.
        /// </summary>
        CompletedInterruptedSwap,

        /// <summary>
        /// Files exist but none of them can be read. Reported rather than deleted — a caller must
        /// decide whether to quarantine or start fresh, and must never do so silently.
        /// </summary>
        Unrecoverable
    }
}
