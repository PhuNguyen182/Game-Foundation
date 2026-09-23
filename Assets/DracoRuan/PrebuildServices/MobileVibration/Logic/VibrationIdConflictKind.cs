namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>The ways two vibration ids can fail to coexist.</summary>
    /// <remarks>
    /// Every kind blocks generation. The case-insensitive ones are not warnings: two ids differing
    /// only by case are a typo in every real project, and they would emit two consts that a call
    /// site cannot tell apart.
    /// </remarks>
    public enum VibrationIdConflictKind
    {
        /// <summary>Two owners declare the same id.</summary>
        DuplicateExact = 0,

        /// <summary>Two owners declare ids that differ only by case.</summary>
        DuplicateIgnoringCase = 1,

        /// <summary>Different ids that generate the same C# member name.</summary>
        MemberCollision = 2,

        /// <summary>Different ids whose generated member names differ only by case.</summary>
        MemberCollisionIgnoringCase = 3,

        /// <summary>The id cannot become a member name at all.</summary>
        Unusable = 4,
    }
}
