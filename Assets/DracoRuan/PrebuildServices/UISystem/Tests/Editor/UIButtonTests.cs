using System;
using DracoRuan.PrebuildServices.UISystem.Components;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class UIButtonTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (this._go != null)
                UnityEngine.Object.DestroyImmediate(this._go);
        }

        private UIButton NewButton()
        {
            this._go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var button = this._go.AddComponent<UIButton>();
            // AddComponent on a loose GameObject does not reliably run Awake synchronously in
            // this Editor/EditMode context (confirmed empirically - see UIButton.EnsurePrepared's
            // doc comment), so tests force it explicitly instead of depending on Awake's timing.
            button.ConfigureForTest(cooldownSeconds: 0f);
            return button;
        }

        private sealed class FakeFeedback : MonoBehaviour, IUIClickFeedback
        {
            public int ClickCount;
            public void OnUIButtonClicked() => this.ClickCount++;
        }

        [Test]
        public void Click_RaisesClickedObservable()
        {
            UIButton button = this.NewButton();
            int received = 0;
            using IDisposable _ = button.Clicked.Subscribe(_ => received++);

            button.Button.onClick.Invoke();

            Assert.AreEqual(1, received);
        }

        [Test]
        public void Click_NotifiesIUIClickFeedbackHooksOnSameGameObject()
        {
            // IUIClickFeedback hooks are collected in EnsurePrepared, so the hook must exist
            // on the GameObject before that runs (ConfigureForTest, via NewButton, forces it).
            this._go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var feedback = this._go.AddComponent<FakeFeedback>();
            UIButton button = this._go.AddComponent<UIButton>();
            button.ConfigureForTest(cooldownSeconds: 0f);

            button.Button.onClick.Invoke();

            Assert.AreEqual(1, feedback.ClickCount);
        }

        [Test]
        public void Interactable_False_MakesWrappedButtonNonInteractable()
        {
            UIButton button = this.NewButton();

            button.Interactable = false;

            Assert.IsFalse(button.Button.interactable);
        }

        [Test]
        public void Interactable_True_AndNoCooldown_MakesWrappedButtonInteractable()
        {
            UIButton button = this.NewButton();

            button.Interactable = false;
            button.Interactable = true;

            Assert.IsTrue(button.Button.interactable);
        }

        [Test]
        public void Click_WithoutCooldownConfigured_DoesNotDisableButton()
        {
            UIButton button = this.NewButton();

            button.Button.onClick.Invoke();

            Assert.IsTrue(button.Button.interactable);
        }

        [Test]
        public void Click_WithCooldownConfigured_DisablesButtonImmediately()
        {
            UIButton button = this.NewButton();
            button.ConfigureForTest(cooldownSeconds: 5f);

            button.Button.onClick.Invoke();

            Assert.IsFalse(button.Button.interactable);
        }

        [Test]
        public void Click_WithCooldownConfigured_SecondClickDuringCooldownDoesNotRaiseClicked()
        {
            UIButton button = this.NewButton();
            button.ConfigureForTest(cooldownSeconds: 5f);
            int received = 0;
            using IDisposable _ = button.Clicked.Subscribe(_ => received++);

            button.Button.onClick.Invoke();
            // Button.interactable already gates a second real click through EventSystem, but
            // HandleClick is also reachable directly (e.g. a UnityEvent already queued before
            // interactable flipped) - assert the cooldown guard itself, not just the Button's
            // own interactable check, catches the repeat.
            button.Button.onClick.Invoke();

            Assert.AreEqual(1, received);
        }

        [Test]
        public void ExternalInteractableFalse_SurvivesCooldownWindow_NoCooldownConfigured()
        {
            // Regression guard for the old BaseUIButton bug (REWRITE_PLAN.md 1.2): an external
            // SetInteractable(false) must never be silently re-enabled by cooldown logic. With
            // no cooldown configured there is nothing to elapse, so this is the simplest case
            // that proves Interactable=false sticks across a click attempt.
            UIButton button = this.NewButton();
            button.Interactable = false;

            button.Button.onClick.Invoke();

            Assert.IsFalse(button.Button.interactable);
        }
    }
}
