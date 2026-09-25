namespace DracoRuan.PrebuildServices.UISystem.Logic
{
    /// <summary>What a layer did in response to a Back/Esc request.</summary>
    public enum UIBackResult
    {
        /// <summary>This layer had nothing to do with Back; try the next layer down.</summary>
        PassThrough,

        /// <summary>This layer closed something (e.g. a popup) in response to Back.</summary>
        Close,

        /// <summary>This layer handled Back without closing anything (e.g. "are you sure?" prompt).</summary>
        Consume,
    }
}
