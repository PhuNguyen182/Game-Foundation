#if UISYSTEM_INPUT_SYSTEM
using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;
using DracoRuan.PrebuildServices.UISystem.Input;
using DracoRuan.PrebuildServices.UISystem.Input.DracoRuan.PrebuildServices.UISystem.InputSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class UIFocusControllerTests
    {
        private GameObject _eventSystemGo;
        private GameObject _modalRootGo;
        private GameObject _insideGo;
        private GameObject _outsideGo;
        private EventSystem _eventSystem;

        [SetUp]
        public void SetUp()
        {
            this._eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
            this._eventSystem = this._eventSystemGo.GetComponent<EventSystem>();

            this._modalRootGo = new GameObject("ModalRoot", typeof(RectTransform));

            this._insideGo = new GameObject("Inside", typeof(RectTransform), typeof(Image), typeof(Button));
            this._insideGo.transform.SetParent(this._modalRootGo.transform);

            this._outsideGo = new GameObject("Outside", typeof(RectTransform), typeof(Image), typeof(Button));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(this._eventSystemGo);
            UnityEngine.Object.DestroyImmediate(this._modalRootGo);
            UnityEngine.Object.DestroyImmediate(this._insideGo);
            UnityEngine.Object.DestroyImmediate(this._outsideGo);
        }

        [Test]
        public void Remember_ThenRestore_ReselectsTheRememberedGameObject()
        {
            this._eventSystem.SetSelectedGameObject(this._outsideGo);
            var focus = new UIFocusController(this._eventSystem);

            focus.Remember();
            this._eventSystem.SetSelectedGameObject(null);
            focus.Restore(defaultSelectable: null);

            Assert.AreEqual(this._outsideGo, this._eventSystem.currentSelectedGameObject);
        }

        [Test]
        public void Restore_RememberedSelectionDestroyed_NoDefaultSelectable_LeavesSelectionNull()
        {
            this._eventSystem.SetSelectedGameObject(this._outsideGo);
            var focus = new UIFocusController(this._eventSystem);
            focus.Remember();

            UnityEngine.Object.DestroyImmediate(this._outsideGo);
            this._outsideGo = null;
            this._eventSystem.SetSelectedGameObject(null);

            focus.Restore(defaultSelectable: null);

            // EventSystem.SetSelectedGameObject(null) leaves currentSelectedGameObject as a
            // Unity "fake null" (a destroyed-object reference, not a true C# null) rather than
            // clearing the field outright - NUnit's Assert.IsNull uses reference/box equality
            // and doesn't respect UnityEngine.Object's == override, so it must be compared with
            // Unity's own operator instead.
            Assert.IsTrue(this._eventSystem.currentSelectedGameObject == null);
        }

        [Test]
        public void ModalScope_SelectionOutsideModalRoot_IsForcedBackToFallback()
        {
            var fallback = this._insideGo.GetComponent<Button>();
            var focus = new UIFocusController(this._eventSystem);
            using System.IDisposable scope = focus.BeginModalScope(this._modalRootGo.transform, fallback);

            this._eventSystem.SetSelectedGameObject(this._outsideGo);
            // RegisterUpdateHandler only queues into a pending list, applied at the START of
            // the NEXT UpdateTime() call (see UpdateServiceManager.UpdateTime) - the handler
            // registered by BeginModalScope above needs one "flush" call before it actually
            // ticks on the second.
            UpdateServiceManager.UpdateTime();
            UpdateServiceManager.UpdateTime();

            Assert.AreEqual(this._insideGo, this._eventSystem.currentSelectedGameObject);
        }

        [Test]
        public void ModalScope_SelectionInsideModalRoot_IsLeftAlone()
        {
            var fallback = this._insideGo.GetComponent<Button>();
            var focus = new UIFocusController(this._eventSystem);
            using System.IDisposable scope = focus.BeginModalScope(this._modalRootGo.transform, fallback);

            this._eventSystem.SetSelectedGameObject(this._insideGo);
            UpdateServiceManager.UpdateTime();
            UpdateServiceManager.UpdateTime();

            Assert.AreEqual(this._insideGo, this._eventSystem.currentSelectedGameObject);
        }

        [Test]
        public void ModalScope_Disposed_StopsClampingSelection()
        {
            var fallback = this._insideGo.GetComponent<Button>();
            var focus = new UIFocusController(this._eventSystem);
            System.IDisposable scope = focus.BeginModalScope(this._modalRootGo.transform, fallback);
            UpdateServiceManager.UpdateTime();

            scope.Dispose();
            this._eventSystem.SetSelectedGameObject(this._outsideGo);
            UpdateServiceManager.UpdateTime();

            Assert.AreEqual(this._outsideGo, this._eventSystem.currentSelectedGameObject);
        }
    }
}
#endif
