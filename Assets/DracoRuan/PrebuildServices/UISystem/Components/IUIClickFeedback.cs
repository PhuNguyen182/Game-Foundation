namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// Extension hook for UIButton: implement on any component on the same GameObject to get
    /// notified of an accepted click, for the game to plug in audio/vibration feedback
    /// (REWRITE_PLAN.md 2.5, "Có hook IUIClickFeedback"). Only raised when the click is
    /// actually accepted (interactable and not cooling down) - not for a click UIButton
    /// itself swallowed.
    /// </summary>
    public interface IUIClickFeedback
    {
        void OnUIButtonClicked();
    }
}
