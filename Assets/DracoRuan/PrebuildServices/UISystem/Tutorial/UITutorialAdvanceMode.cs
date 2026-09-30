namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Tutorial
{
    /// <summary>How a UITutorialStep hands control to the next step.</summary>
    public enum UITutorialAdvanceMode
    {
        /// <summary>Waits for the player to click inside the highlighted area (the mask's own
        /// raycast hole - see UITutorialMaskController).</summary>
        ClickHighlight,

        /// <summary>Waits for the player to click anywhere (a "tap to continue" dialogue box,
        /// no highlight interaction required).</summary>
        ClickAnywhere,

        /// <summary>Advances on its own after `autoAdvanceSeconds`.</summary>
        Timer,

        /// <summary>Waits for external code to call UITutorialRunner.AdvanceCurrentStep() -
        /// for a step gated on gameplay (e.g. "wait until the player equips an item").</summary>
        External,
    }
}
