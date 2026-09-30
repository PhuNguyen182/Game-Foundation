using System;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Input
{
    /// <summary>
    /// Engine-agnostic seam UIService attaches to for PC/Console focus behavior (REWRITE_PLAN.md
    /// 2.5, "Focus (PC/Console)"), mirroring IUIBackInputSource: UIService references only this
    /// interface (no UISYSTEM_INPUT_SYSTEM gate needed here), while UIFocusController - the
    /// actual implementation, which needs InputActionReference/Gamepad/Keyboard - stays behind
    /// that define. A game not using Input System simply never attaches one, and UIService's
    /// focus calls become no-ops (see UIService.AttachFocusHandler).
    /// </summary>
    public interface IUIFocusHandler
    {
        void Remember();
        void Restore(Selectable defaultSelectable);
        IDisposable BeginModalScope(Transform modalRoot, Selectable fallbackSelectable);
    }
}
