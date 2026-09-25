using System;
using DracoRuan.PrebuildServices.UISystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class InputLockCounterTests
    {
        private InputLockCounter _counter;

        [SetUp]
        public void SetUp() => this._counter = new InputLockCounter();

        [Test]
        public void IsLocked_NoAcquire_IsFalse()
        {
            Assert.That(this._counter.IsLocked, Is.False);
        }

        [Test]
        public void IsLocked_AfterAcquire_IsTrue()
        {
            this._counter.Acquire();

            Assert.That(this._counter.IsLocked, Is.True);
        }

        [Test]
        public void IsLocked_TwoAcquiresOneRelease_StillLocked()
        {
            this._counter.Acquire();
            this._counter.Acquire();
            this._counter.Release();

            Assert.That(this._counter.IsLocked, Is.True);
        }

        [Test]
        public void IsLocked_TwoAcquiresTwoReleases_Unlocked()
        {
            this._counter.Acquire();
            this._counter.Acquire();
            this._counter.Release();
            this._counter.Release();

            Assert.That(this._counter.IsLocked, Is.False);
        }

        [Test]
        public void Release_WithoutMatchingAcquire_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => this._counter.Release());
        }
    }
}
