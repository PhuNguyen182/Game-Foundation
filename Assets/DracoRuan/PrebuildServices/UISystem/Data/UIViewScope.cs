namespace DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>
    /// Whether a popup belongs to the current screen (closed automatically when the screen
    /// changes) or is global (survives screen changes, e.g. a system-wide notification).
    /// </summary>
    public enum UIViewScope
    {
        Screen,
        Global,
    }
}
