namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>
    /// What the planner concluded about one domain.
    /// </summary>
    /// <remarks>
    /// Domains are classified independently on purpose. A missing migrator for the quest data must
    /// not stop the inventory from upgrading, and it must certainly not stop the game from booting —
    /// only domains that actually depend on the broken one are affected. The previous engine threw
    /// on the first problem it met while iterating every registered domain, so one gap took the
    /// whole boot down.
    /// </remarks>
    public enum DomainPlanStatus
    {
        /// <summary>On-disk version already equals the target. Nothing to do.</summary>
        UpToDate = 0,

        /// <summary>No save file exists. A fresh install; the controller creates defaults.</summary>
        NoSaveData,

        /// <summary>A complete chain of steps exists and is scheduled.</summary>
        Migrate,

        /// <summary>
        /// No step advances the domain from some intermediate version, so the chain cannot be
        /// completed. The save is left untouched.
        /// </summary>
        MissingMigrator,

        /// <summary>
        /// On-disk version is higher than this build supports — the player installed an older build,
        /// restored a newer cloud save, or rolled back a store release.
        /// </summary>
        /// <remarks>
        /// Never treat this as up to date. The payload would deserialize "successfully" into the
        /// older class, dropping every field added since, and the next autosave would write that
        /// loss back permanently.
        /// </remarks>
        Downgrade,

        /// <summary>The save file exists but does not decode. Quarantine rather than overwrite.</summary>
        Corrupt,

        /// <summary>
        /// The domain sits in a dependency cycle that no group step resolves, so no correct order
        /// exists. Reported against every member of the cycle.
        /// </summary>
        UnresolvableCycle,

        /// <summary>
        /// The domain is fine in itself but depends on one that is not, so migrating it would read
        /// data that was never upgraded.
        /// </summary>
        BlockedByDependency
    }
}
