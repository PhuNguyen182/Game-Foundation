using System;

namespace DracoRuan.PrebuildServices.UISystem.Logic.Badge
{
    /// <summary>
    /// REWRITE_PLAN.md mục 5 bước 10 (tuỳ chọn): "Badge/red-dot service (VM thuần, cây key →
    /// count)". This is the abstract contract only - by design (see PROGRESS.md), not a concrete
    /// implementation, so a game can back it with whatever storage/sync strategy it needs
    /// (in-memory, save-file-backed, server-driven) without UISystem dictating one.
    ///
    /// Keys are hierarchical paths (see BadgeKey) so a parent node's count is the sum of its own
    /// count plus every descendant's - e.g. setting "Shop/Weapons/Sword" also changes what
    /// "Shop/Weapons" and "Shop" report, without the caller manually rolling counts up by hand.
    /// Pure C# (no UnityEngine, no R3): a UI layer binds to this through its own adapter (see
    /// REWRITE_PLAN.md's IUITextLocalizer for the same "adapter at the boundary" shape), which is
    /// exactly what keeps this asmdef noEngineReferences and independently testable.
    /// </summary>
    public interface IBadgeService
    {
        /// <summary>Sets this exact key's own count (not counting descendants). Negative values
        /// are not meaningful for a badge and implementations should clamp to 0.</summary>
        void SetCount(BadgeKey key, int count);

        /// <summary>This key's own count plus every descendant's, recursively.</summary>
        int GetCount(BadgeKey key);

        /// <summary>True if GetCount(key) &gt; 0 - the common case callers actually want (show/hide
        /// a red dot), spelled out so callers don't repeat "> 0" everywhere.</summary>
        bool HasAny(BadgeKey key);

        /// <summary>Fires whenever a SetCount call changes what GetCount(key) or any of its
        /// ancestors would return - i.e. the rolled-up total, not just an exact-key write.
        /// Subscribing to a parent key observes changes anywhere in its subtree.</summary>
        event Action<BadgeKey> Changed;
    }
}
