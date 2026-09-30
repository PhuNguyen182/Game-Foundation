using System;

namespace DracoRuan.PrebuildServices.UISystem.Input
{
    /// <summary>
    /// Fires when the player asks to move to the previous/next tab (gamepad LB/RB, Q/E, ...).
    /// UITabGroup subscribes to this instead of any concrete input backend, so the core assembly
    /// stays free of Unity.InputSystem; a game on Rewired/legacy input implements it itself.
    /// </summary>
    public interface IUITabNavigationSource
    {
        event Action Previous;
        event Action Next;
    }
}
