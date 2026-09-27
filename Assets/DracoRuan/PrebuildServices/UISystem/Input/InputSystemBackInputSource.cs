#if UISYSTEM_INPUT_SYSTEM
using System;
using UnityEngine.InputSystem;

namespace DracoRuan.PrebuildServices.UISystem.Input
{
    /// <summary>
    /// Default IUIBackInputSource (REWRITE_PLAN.md 2.5): listens to the "UI/Cancel" action from
    /// Unity's default UI action map (Esc, gamepad B/Circle, Android back all map to it out of
    /// the box). Not a MonoBehaviour - PlayerLoop-driven via a manual Update subscription so it
    /// works the same whether the game wires it as a plain object or wraps it in a component.
    /// </summary>
    public sealed class InputSystemBackInputSource : IUIBackInputSource, IDisposable
    {
        private readonly InputAction _cancelAction;
        private readonly bool _ownsAction;

        public event Action BackRequested;

        /// <param name="cancelAction">Pass an existing "UI/Cancel"-bound action (e.g. from the
        /// project's InputActionAsset) to share its enable/disable lifecycle with the rest of
        /// the game's input; omit to create and own a private one bound to
        /// "*/{Cancel}" (works with Esc, gamepad East button, and Android back via the
        /// Cancel-binding Unity's default UI map ships).</param>
        public InputSystemBackInputSource(InputAction cancelAction = null)
        {
            if (cancelAction != null)
            {
                this._cancelAction = cancelAction;
                this._ownsAction = false;
            }
            else
            {
                this._cancelAction = new InputAction("UISystem/Cancel", InputActionType.Button, "*/{Cancel}");
                this._ownsAction = true;
                this._cancelAction.Enable();
            }

            this._cancelAction.performed += this.OnCancelPerformed;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context) => this.BackRequested?.Invoke();

        public void Dispose()
        {
            this._cancelAction.performed -= this.OnCancelPerformed;
            if (this._ownsAction)
                this._cancelAction.Dispose();
        }
    }
}
#endif
