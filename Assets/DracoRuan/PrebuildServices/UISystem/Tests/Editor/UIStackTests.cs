using DracoRuan.PrebuildServices.UISystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class UIStackTests
    {
        private UIStack<string> _stack;

        [SetUp]
        public void SetUp() => this._stack = new UIStack<string>();

        [Test]
        public void Push_ThenPeek_ReturnsPushedItem()
        {
            this._stack.Push("A");

            bool hasTop = this._stack.TryPeek(out string top);

            Assert.That(hasTop, Is.True);
            Assert.That(top, Is.EqualTo("A"));
        }

        [Test]
        public void TryPop_OnEmptyStack_ReturnsFalse()
        {
            bool popped = this._stack.TryPop(out _);

            Assert.That(popped, Is.False);
        }

        [Test]
        public void Push_ThenPop_ReturnsLastPushedItem_LifoOrder()
        {
            this._stack.Push("A");
            this._stack.Push("B");

            this._stack.TryPop(out string popped);

            Assert.That(popped, Is.EqualTo("B"));
            this._stack.TryPeek(out string top);
            Assert.That(top, Is.EqualTo("A"));
        }

        [Test]
        public void TryReplace_WithItemsPresent_SwapsTopWithoutGrowingHistory()
        {
            this._stack.Push("A");
            this._stack.Push("B");

            bool replaced = this._stack.TryReplace("C", out string oldTop);

            Assert.That(replaced, Is.True);
            Assert.That(oldTop, Is.EqualTo("B"));
            Assert.That(this._stack.Count, Is.EqualTo(2));
            this._stack.TryPeek(out string top);
            Assert.That(top, Is.EqualTo("C"));
        }

        [Test]
        public void PopToRoot_LeavesOnlyFirstPushedItem_ReturnsPoppedInLifoOrder()
        {
            this._stack.Push("A");
            this._stack.Push("B");
            this._stack.Push("C");

            var popped = this._stack.PopToRoot();

            Assert.That(popped, Is.EqualTo(new[] { "C", "B" }));
            Assert.That(this._stack.Count, Is.EqualTo(1));
            this._stack.TryPeek(out string top);
            Assert.That(top, Is.EqualTo("A"));
        }
    }
}
