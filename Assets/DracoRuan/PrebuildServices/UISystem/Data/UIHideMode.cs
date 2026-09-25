namespace DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>How a KeepAlive view is hidden without destroying it.</summary>
    public enum UIHideMode
    {
        /// <summary>
        /// Default: Canvas.enabled + GraphicRaycaster.enabled = false, GameObject stays
        /// active. Avoids OnDisable/OnEnable, layout rebuild, and TMP regeneration.
        /// </summary>
        DisableCanvas,

        /// <summary>GameObject.SetActive(false). Use when the view has Update logic to stop.</summary>
        Deactivate,
    }
}
