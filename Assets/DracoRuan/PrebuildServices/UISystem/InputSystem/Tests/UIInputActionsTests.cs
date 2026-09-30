using DracoRuan.PrebuildServices.UISystem.Input;
using DracoRuan.PrebuildServices.UISystem.Input.DracoRuan.PrebuildServices.UISystem.InputSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class UIInputActionsTests
    {
        private InputActionAsset _original;
        private InputActionAsset _clone;
        private InputActionReference _reference;

        [SetUp]
        public void SetUp()
        {
            this._original = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = this._original.AddActionMap("Menu");
            map.AddAction("GoBack", InputActionType.Button, "<Keyboard>/escape");

            // PlayerInput clones its asset per player; Instantiate reproduces that.
            this._clone = Object.Instantiate(this._original);
            this._reference = InputActionReference.Create(this._original.FindAction("Menu/GoBack"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this._reference);
            Object.DestroyImmediate(this._clone);
            Object.DestroyImmediate(this._original);
        }

        [Test]
        public void Resolve_WithoutRuntimeAsset_ReturnsReferencedAction()
        {
            InputAction action = UIInputActions.Resolve(this._reference, null);

            Assert.AreSame(this._reference.action, action);
        }

        [Test]
        public void Resolve_WithRuntimeAsset_ReturnsSameIdActionFromClone()
        {
            InputAction action = UIInputActions.Resolve(this._reference, this._clone);

            Assert.AreEqual(this._reference.action.id, action.id);
            Assert.AreSame(this._clone, action.actionMap.asset);
            Assert.AreNotSame(this._reference.action, action);
        }

        [Test]
        public void Resolve_NullReference_ReturnsNull()
        {
            Assert.IsNull(UIInputActions.Resolve(null, this._clone));
        }

        [Test]
        public void InputSystemBackInputSource_BorrowedResolvedAction_NotDisposedOnDispose()
        {
            InputAction action = UIInputActions.Resolve(this._reference, this._clone);
            var source = new InputSystemBackInputSource(action);
            int requests = 0;
            source.BackRequested += () => requests++;

            source.Dispose();

            Assert.IsNotNull(action.actionMap, "A borrowed action must survive the source being disposed.");
            Assert.AreEqual(0, requests);
        }
    }
}
