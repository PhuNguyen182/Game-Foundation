namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>What happens to a view's spawned instance when it closes.</summary>
    public enum UICachePolicy
    {
        /// <summary>Instance is released (destroyed / returned) when it closes.</summary>
        Destroy,

        /// <summary>Instance is hidden (see UIHideMode) and reused next time it opens.</summary>
        KeepAlive,
    }
}
