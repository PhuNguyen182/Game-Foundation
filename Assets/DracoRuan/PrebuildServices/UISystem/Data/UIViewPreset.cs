namespace DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>
    /// A preset only fills in default values for a UIViewDefinition's flags (layer, hidesBelow,
    /// modal, etc. — see UIViewDefinition). View is the only entity the router manages; a preset
    /// never becomes its own class. Every flag can still be overridden per-view.
    /// </summary>
    public enum UIViewPreset
    {
        Screen,
        Popup,
        Overlay,
        Toast,
        Hint,
        System,
        Tutorial,
    }
}
