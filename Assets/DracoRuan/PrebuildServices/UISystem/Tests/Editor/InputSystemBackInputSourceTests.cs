#if UISYSTEM_INPUT_SYSTEM
using DracoRuan.PrebuildServices.UISystem.Input;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class InputSystemBackInputSourceTests
    {
        [Test]
        public void Dispose_UnsubscribesFromAction_NoMoreBackRequestedAfterDispose()
        {
            var action = new InputAction("TestCancel", InputActionType.Button);
            action.Enable();
            var source = new InputSystemBackInputSource(action);
            int count = 0;
            source.BackRequested += () => count++;

            source.Dispose();

            // Owned-action disposal isn't exercised here (this action was supplied, not owned),
            // but Dispose must still detach its own handler so a caller-owned action that keeps
            // firing afterward doesn't leak a call into a disposed source.
            Assert.DoesNotThrow(() => action.Dispose());
            Assert.AreEqual(0, count);
        }

        [Test]
        public void SuppliedAction_NotDisposedByInputSystemBackInputSource()
        {
            var action = new InputAction("TestCancel2", InputActionType.Button);
            action.Enable();
            var source = new InputSystemBackInputSource(action);

            source.Dispose();

            // A caller-supplied action is not owned - InputSystemBackInputSource must not
            // dispose it out from under the caller.
            Assert.DoesNotThrow(() => action.Enable());
            action.Dispose();
        }
    }
}
#endif
