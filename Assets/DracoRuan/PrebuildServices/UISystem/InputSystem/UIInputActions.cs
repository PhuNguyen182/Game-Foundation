using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DracoRuan.PrebuildServices.UISystem.Input.DracoRuan.PrebuildServices.UISystem.InputSystem
{
    /// <summary>
    /// The single place a game tells UISystem which of its own actions drive the UI
    /// (CAMERA_INPUT_PLAN.md, Quyết định 4). Fields are InputActionReferences, which store the
    /// action GUID, so renaming maps/actions/bindings in the game's asset never breaks them.
    /// Empty fields fall back to defaults (Unity default UI actions for the input module,
    /// "*/{Cancel}" for Back, gamepad shoulders for tabs). The same asset configures the
    /// InputSystemUIInputModule and the Back/Focus/Tab sources.
    /// </summary>
    [CreateAssetMenu(fileName = "UIInputActions", menuName = "DracoRuan/UISystem/UI Input Actions")]
    public sealed class UIInputActions : ScriptableObject
    {
        [Header("Input module")]
        public InputActionReference point;
        public InputActionReference click;
        public InputActionReference middleClick;
        public InputActionReference rightClick;
        public InputActionReference scroll;
        public InputActionReference navigate;
        public InputActionReference submit;

        [Header("UISystem")]
        public InputActionReference cancel;
        public InputActionReference previousTab;
        public InputActionReference nextTab;

        /// <summary>
        /// Maps a reference to the action instance that is actually live. A PlayerInput clones its
        /// asset per player, so a reference into the original asset never receives events; pass
        /// PlayerInput.actions as <paramref name="runtimeAsset"/> to resolve by action id in the
        /// clone instead. Null runtimeAsset (or reference) resolves to the reference as is.
        /// </summary>
        public static InputAction Resolve(InputActionReference reference, InputActionAsset runtimeAsset)
        {
            if (reference == null)
                return null;

            InputAction original = reference.action;
            if (runtimeAsset == null || original == null)
                return original;

            return runtimeAsset.FindAction(original.id.ToString()) ?? original;
        }

        /// <summary>Same as <see cref="Resolve"/> but returns a reference. When the action was
        /// remapped into a runtime asset a new reference is created and reported through
        /// <paramref name="created"/> so the caller can destroy it.</summary>
        public static InputActionReference ResolveReference(InputActionReference reference,
            InputActionAsset runtimeAsset, out bool created)
        {
            created = false;
            if (reference == null)
                return null;

            InputAction resolved = Resolve(reference, runtimeAsset);
            if (resolved == null || resolved == reference.action)
                return reference;

            created = true;
            return InputActionReference.Create(resolved);
        }

        /// <summary>Points an InputSystemUIInputModule at these actions. Unset fields keep the
        /// module's default UI actions.</summary>
        public void ApplyTo(InputSystemUIInputModule module, InputActionAsset runtimeAsset,
            System.Collections.Generic.List<InputActionReference> createdReferences = null)
        {
            module.AssignDefaultActions();

            InputActionReference anyReference = this.point ?? this.click ?? this.navigate ?? this.submit
                                                ?? this.scroll ?? this.rightClick ?? this.middleClick;
            if (runtimeAsset != null)
                module.actionsAsset = runtimeAsset;
            else if (anyReference != null && anyReference.asset != null)
                module.actionsAsset = anyReference.asset;

            module.point = this.Pick(this.point, module.point, runtimeAsset, createdReferences);
            module.leftClick = this.Pick(this.click, module.leftClick, runtimeAsset, createdReferences);
            module.middleClick = this.Pick(this.middleClick, module.middleClick, runtimeAsset, createdReferences);
            module.rightClick = this.Pick(this.rightClick, module.rightClick, runtimeAsset, createdReferences);
            module.scrollWheel = this.Pick(this.scroll, module.scrollWheel, runtimeAsset, createdReferences);
            module.move = this.Pick(this.navigate, module.move, runtimeAsset, createdReferences);
            module.submit = this.Pick(this.submit, module.submit, runtimeAsset, createdReferences);
            module.cancel = this.Pick(this.cancel, module.cancel, runtimeAsset, createdReferences);
        }

        private InputActionReference Pick(InputActionReference configured, InputActionReference current,
            InputActionAsset runtimeAsset, System.Collections.Generic.List<InputActionReference> createdReferences)
        {
            if (configured == null)
                return current;

            InputActionReference resolved = ResolveReference(configured, runtimeAsset, out bool created);
            if (created)
                createdReferences?.Add(resolved);

            return resolved;
        }
    }
}
