using System;

namespace DracoRuan.PrebuildServices.UISystem.Input
{
    /// <summary>
    /// Fires whenever the game's "Back" gesture happens (Esc, gamepad B/Circle, Android back).
    /// UIService.HandleBackInput() is the actual router entry point; a source just decides WHEN
    /// to call it, decoupling BackRouter from any specific input backend (REWRITE_PLAN.md 2.5).
    /// </summary>
    public interface IUIBackInputSource
    {
        event Action BackRequested;
    }
}
