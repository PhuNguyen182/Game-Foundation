using DracoRuan.PrebuildServices.UISystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class UIViewStateMachineTests
    {
        private UIViewStateMachine _machine;

        [SetUp]
        public void SetUp() => this._machine = new UIViewStateMachine();

        [Test]
        public void InitialState_IsHidden()
        {
            Assert.That(this._machine.State, Is.EqualTo(UIViewState.Hidden));
        }

        [Test]
        public void TryBeginShow_FromHidden_SucceedsAndMovesToShowing()
        {
            bool started = this._machine.TryBeginShow();

            Assert.That(started, Is.True);
            Assert.That(this._machine.State, Is.EqualTo(UIViewState.Showing));
        }

        [Test]
        public void TryBeginShow_CalledTwice_SecondCallIsRejected()
        {
            this._machine.TryBeginShow();
            bool secondStart = this._machine.TryBeginShow();

            Assert.That(secondStart, Is.False);
            Assert.That(this._machine.State, Is.EqualTo(UIViewState.Showing));
        }

        [Test]
        public void TryBeginHide_WhileShowing_IsRejected_BecauseShowHasNotCompleted()
        {
            this._machine.TryBeginShow();
            bool hideStarted = this._machine.TryBeginHide();

            Assert.That(hideStarted, Is.False);
            Assert.That(this._machine.State, Is.EqualTo(UIViewState.Showing));
        }

        [Test]
        public void FullCycle_Show_Complete_Hide_Complete_ReturnsToHidden()
        {
            Assert.That(this._machine.TryBeginShow(), Is.True);
            Assert.That(this._machine.TryCompleteShow(), Is.True);
            Assert.That(this._machine.State, Is.EqualTo(UIViewState.Shown));

            Assert.That(this._machine.TryBeginHide(), Is.True);
            Assert.That(this._machine.State, Is.EqualTo(UIViewState.Hiding));

            Assert.That(this._machine.TryCompleteHide(), Is.True);
            Assert.That(this._machine.State, Is.EqualTo(UIViewState.Hidden));
        }

        [Test]
        public void TryCompleteShow_WithoutBeginShow_IsRejected()
        {
            Assert.That(this._machine.TryCompleteShow(), Is.False);
            Assert.That(this._machine.State, Is.EqualTo(UIViewState.Hidden));
        }

        [Test]
        public void TryBeginShow_WhileHiding_IsRejected_BecauseHideHasNotCompleted()
        {
            this._machine.TryBeginShow();
            this._machine.TryCompleteShow();
            this._machine.TryBeginHide();

            bool reShowWhileHiding = this._machine.TryBeginShow();

            Assert.That(reShowWhileHiding, Is.False);
            Assert.That(this._machine.State, Is.EqualTo(UIViewState.Hiding));
        }
    }
}
