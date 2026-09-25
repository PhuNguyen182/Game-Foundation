using DracoRuan.PrebuildServices.UISystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class UIPopupQueueTests
    {
        private UIPopupQueue<string> _queue;

        [SetUp]
        public void SetUp() => this._queue = new UIPopupQueue<string>();

        [Test]
        public void TryDequeue_OnEmptyQueue_ReturnsFalse()
        {
            bool dequeued = this._queue.TryDequeue(out _);

            Assert.That(dequeued, Is.False);
        }

        [Test]
        public void TryDequeue_SamePriority_ReturnsFifoOrder()
        {
            this._queue.Enqueue("A", priority: 0);
            this._queue.Enqueue("B", priority: 0);
            this._queue.Enqueue("C", priority: 0);

            this._queue.TryDequeue(out string first);
            this._queue.TryDequeue(out string second);
            this._queue.TryDequeue(out string third);

            Assert.That(first, Is.EqualTo("A"));
            Assert.That(second, Is.EqualTo("B"));
            Assert.That(third, Is.EqualTo("C"));
        }

        [Test]
        public void TryDequeue_MixedPriority_HigherPriorityFirst_ThenFifoWithinSamePriority()
        {
            this._queue.Enqueue("Low", priority: 0);
            this._queue.Enqueue("HighFirst", priority: 5);
            this._queue.Enqueue("HighSecond", priority: 5);

            this._queue.TryDequeue(out string first);
            this._queue.TryDequeue(out string second);
            this._queue.TryDequeue(out string third);

            Assert.That(first, Is.EqualTo("HighFirst"));
            Assert.That(second, Is.EqualTo("HighSecond"));
            Assert.That(third, Is.EqualTo("Low"));
        }

        [Test]
        public void TryDequeue_WhilePaused_ReturnsFalseEvenIfItemsQueued()
        {
            this._queue.Enqueue("A");
            this._queue.Pause();

            bool dequeued = this._queue.TryDequeue(out _);

            Assert.That(dequeued, Is.False);
            Assert.That(this._queue.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryDequeue_AfterResume_ReturnsQueuedItem()
        {
            this._queue.Enqueue("A");
            this._queue.Pause();
            this._queue.Resume();

            bool dequeued = this._queue.TryDequeue(out string item);

            Assert.That(dequeued, Is.True);
            Assert.That(item, Is.EqualTo("A"));
        }
    }
}
