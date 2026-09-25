using DracoRuan.PrebuildServices.UISystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class SortOrderAllocatorTests
    {
        private SortOrderAllocator _allocator;

        [SetUp]
        public void SetUp() => this._allocator = new SortOrderAllocator(baseSortOrder: 10, step: 10);

        [Test]
        public void Allocate_Sequentially_ReturnsIncreasingSlots()
        {
            int first = this._allocator.Allocate();
            int second = this._allocator.Allocate();
            int third = this._allocator.Allocate();

            Assert.That(first, Is.EqualTo(10));
            Assert.That(second, Is.EqualTo(20));
            Assert.That(third, Is.EqualTo(30));
        }

        [Test]
        public void Allocate_AfterReleasingTopSlot_ReusesThatSlot()
        {
            this._allocator.Allocate();
            this._allocator.Allocate();
            int top = this._allocator.Allocate();

            this._allocator.Release(top);
            int reused = this._allocator.Allocate();

            Assert.That(reused, Is.EqualTo(top));
        }

        [Test]
        public void Allocate_AfterOutOfOrderRelease_ReusesReleasedSlotBeforeGrowing()
        {
            int a = this._allocator.Allocate();
            int b = this._allocator.Allocate();
            int c = this._allocator.Allocate();
            int d = this._allocator.Allocate();

            this._allocator.Release(b); // out-of-order: not the current top (d)

            int reused = this._allocator.Allocate();

            Assert.That(reused, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(c, Is.Not.EqualTo(d));
        }

        [Test]
        public void Release_TopSlot_ThenReleasePreviousSlot_CoalescesBackToBase()
        {
            int a = this._allocator.Allocate(); // 10
            int b = this._allocator.Allocate(); // 20

            this._allocator.Release(b); // top -> next slot shrinks to 20
            this._allocator.Release(a); // now top -> next slot shrinks to 10

            int reused = this._allocator.Allocate();

            Assert.That(reused, Is.EqualTo(a));
        }
    }
}
