using System;
using UnityEngine.InputSystem;

namespace DracoRuan.PrebuildServices.UISystem.Input.DracoRuan.PrebuildServices.UISystem.InputSystem
{
    /// <summary>
    /// Input System backed IUITabNavigationSource. Own-or-borrow like InputSystemBackInputSource:
    /// actions passed in belong to the game (never enabled/disabled/disposed here); when either is
    /// omitted, private gamepad-shoulder actions are created, enabled and disposed by this object.
    /// </summary>
    public sealed class InputSystemTabNavigationSource : IUITabNavigationSource, IDisposable
    {
        private readonly InputAction _previousAction;
        private readonly InputAction _nextAction;
        private readonly bool _ownsActions;

        public event Action Previous;
        public event Action Next;

        public InputSystemTabNavigationSource(InputAction previousAction = null, InputAction nextAction = null)
        {
            if (previousAction != null && nextAction != null)
            {
                this._previousAction = previousAction;
                this._nextAction = nextAction;
            }
            else
            {
                this._previousAction = new InputAction("UITabGroup/Previous", InputActionType.Button, "<Gamepad>/leftShoulder");
                this._nextAction = new InputAction("UITabGroup/Next", InputActionType.Button, "<Gamepad>/rightShoulder");
                this._previousAction.Enable();
                this._nextAction.Enable();
                this._ownsActions = true;
            }

            this._previousAction.performed += this.OnPrevious;
            this._nextAction.performed += this.OnNext;
        }

        public void Dispose()
        {
            this._previousAction.performed -= this.OnPrevious;
            this._nextAction.performed -= this.OnNext;

            if (!this._ownsActions)
                return;

            this._previousAction.Dispose();
            this._nextAction.Dispose();
        }

        private void OnPrevious(InputAction.CallbackContext context) => this.Previous?.Invoke();

        private void OnNext(InputAction.CallbackContext context) => this.Next?.Invoke();
    }
}
