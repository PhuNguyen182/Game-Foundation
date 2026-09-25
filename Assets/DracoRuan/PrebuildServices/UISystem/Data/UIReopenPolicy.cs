namespace DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>What happens when a view is opened while an instance of it is already open.</summary>
    public enum UIReopenPolicy
    {
        /// <summary>Bring the existing instance to the front instead of opening another.</summary>
        BringToFront,

        /// <summary>The new open request is ignored; the existing instance is left as-is.</summary>
        Ignore,

        /// <summary>Multiple instances may be open at once (e.g. stackable toasts).</summary>
        AllowMultiple,
    }
}
