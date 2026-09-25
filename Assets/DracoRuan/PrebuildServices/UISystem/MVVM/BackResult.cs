namespace DracoRuan.PrebuildServices.UISystem.MVVM
{
    /// <summary>What a view model wants to happen in response to a Back/Esc request.</summary>
    public enum BackResult
    {
        /// <summary>Close this view (the default: back closes the topmost screen/popup).</summary>
        Close,

        /// <summary>Handled internally without closing (e.g. an "are you sure?" prompt shown first).</summary>
        Consume,
    }
}
