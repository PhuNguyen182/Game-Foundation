#if UISYSTEM_INPUT_SYSTEM
using System;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Input
{
    /// <summary>
    /// PC/Console focus behavior (REWRITE_PLAN.md 2.5, "Focus (PC/Console)"): remembers
    /// EventSystem.currentSelectedGameObject across a view's blur/focus, only auto-selects when
    /// the last input device that produced a navigation event was a keyboard or gamepad (mouse/
    /// touch users see no selection highlight), and can clamp EventSystem navigation so it can't
    /// leave a modal view's root. Not itself wired to UIService - a game calls Remember/Restore
    /// from UIViewBase.OnBlurred/OnFocused (or OnOpening/OnClosing) and BeginModalScope/
    /// EndModalScope around a modal view's lifetime.
    /// </summary>
    public sealed class UIFocusController : IUIFocusHandler, IDisposable
    {
        private readonly EventSystem _eventSystem;
        private readonly InputActionReference[] _navigationActions;
        private GameObject _rememberedSelection;
        private bool _lastDeviceIsKeyboardOrGamepad;

        /// <param name="navigationActions">Actions whose performed callback marks the last
        /// device as keyboard/gamepad-driven when the triggering control belongs to one (e.g.
        /// the project's "UI/Navigate" and "UI/Submit" actions). Pass none to skip
        /// device-type tracking entirely (auto-select then never happens).</param>
        public UIFocusController(EventSystem eventSystem, params InputActionReference[] navigationActions)
        {
            this._eventSystem = eventSystem;
            this._navigationActions = navigationActions ?? Array.Empty<InputActionReference>();

            foreach (InputActionReference reference in this._navigationActions)
            {
                if (reference?.action != null)
                    reference.action.performed += this.OnNavigationPerformed;
            }
        }

        public void Dispose()
        {
            foreach (InputActionReference reference in this._navigationActions)
            {
                if (reference?.action != null)
                    reference.action.performed -= this.OnNavigationPerformed;
            }
        }

        private void OnNavigationPerformed(InputAction.CallbackContext context)
        {
            InputDevice device = context.control?.device;
            this._lastDeviceIsKeyboardOrGamepad = device is Keyboard || device is Gamepad;
        }

        /// <summary>Call when a view blurs (another view opens above it, or it's about to
        /// close): remembers the current selection so Restore can bring it back.</summary>
        public void Remember() => this._rememberedSelection = this._eventSystem.currentSelectedGameObject;

        /// <summary>
        /// Call when a view becomes topmost again (focused, or just opened). Restores the
        /// remembered selection if it's still valid (not destroyed/inactive); falls back to
        /// `defaultSelectable` only when the last-used device was keyboard/gamepad - a mouse/
        /// touch user never gets an unsolicited selection highlight.
        /// </summary>
        public void Restore(Selectable defaultSelectable)
        {
            if (this._rememberedSelection != null && this._rememberedSelection.activeInHierarchy)
            {
                this._eventSystem.SetSelectedGameObject(this._rememberedSelection);
                return;
            }

            if (this._lastDeviceIsKeyboardOrGamepad && defaultSelectable != null)
                this._eventSystem.SetSelectedGameObject(defaultSelectable.gameObject);
        }

        /// <summary>
        /// Clamps EventSystem navigation to `modalRoot` for as long as it stays active
        /// (REWRITE_PLAN.md: "Chặn navigation thoát ra khỏi view modal") - every frame, if the
        /// current selection isn't a descendant of modalRoot, it's forced back onto
        /// `fallbackSelectable`. Cheap because it only runs while a modal is open; the caller
        /// (UIViewBase.OnOpening/OnClosing pairing) is responsible for calling this once and
        /// disposing the returned handle when the modal closes.
        /// </summary>
        public IDisposable BeginModalScope(Transform modalRoot, Selectable fallbackSelectable) =>
            new ModalScope(this._eventSystem, modalRoot, fallbackSelectable);

        private sealed class ModalScope : IUpdateHandler, IDisposable
        {
            private readonly EventSystem _eventSystem;
            private readonly Transform _modalRoot;
            private readonly Selectable _fallbackSelectable;
            private bool _disposed;

            public ModalScope(EventSystem eventSystem, Transform modalRoot, Selectable fallbackSelectable)
            {
                this._eventSystem = eventSystem;
                this._modalRoot = modalRoot;
                this._fallbackSelectable = fallbackSelectable;
                UpdateServiceManager.RegisterUpdateHandler(this);
            }

            void IUpdateHandler.Tick(float deltaTime)
            {
                GameObject current = this._eventSystem.currentSelectedGameObject;
                if (current != null && current.transform.IsChildOf(this._modalRoot))
                    return;

                if (this._fallbackSelectable != null)
                    this._eventSystem.SetSelectedGameObject(this._fallbackSelectable.gameObject);
            }

            public void Dispose()
            {
                if (this._disposed)
                    return;

                this._disposed = true;
                UpdateServiceManager.DeregisterUpdateHandler(this);
            }
        }
    }
}
#endif
