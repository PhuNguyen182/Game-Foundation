using System;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects the string-to-index table that makes entry lookup O(1) at runtime.
    /// </summary>
    /// <remarks>
    /// The index, not the id, is the key everything downstream uses — fire-rate timestamps,
    /// concurrency counts, clip leases — so one dictionary lookup per <c>Play</c> buys an array
    /// index for the rest of the request. Duplicate ids throw rather than overwrite, and the
    /// message names both owners, matching how <c>StaticDataRegistry</c> reports the same mistake.
    /// </remarks>
    [TestFixture]
    public sealed class AudioIdRegistryTests
    {
        private AudioIdRegistry _registry;

        [SetUp]
        public void SetUp() => this._registry = new AudioIdRegistry();

        [Test]
        public void Add_ReturnsIndicesInRegistrationOrder()
        {
            Assert.That(this._registry.Add("Click", "Assets/A.asset"), Is.EqualTo(0));
            Assert.That(this._registry.Add("Boom", "Assets/B.asset"), Is.EqualTo(1));
        }

        [Test]
        public void TryGetIndex_ARegisteredId_ReturnsItsIndex()
        {
            this._registry.Add("Click", "Assets/A.asset");
            this._registry.Add("Boom", "Assets/B.asset");

            Assert.That(this._registry.TryGetIndex("Boom", out int index), Is.True);
            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void TryGetIndex_AnUnknownId_IsFalse()
        {
            Assert.That(this._registry.TryGetIndex("Missing", out int index), Is.False);
            Assert.That(index, Is.EqualTo(-1), "An out value of 0 would silently resolve to the first entry.");
        }

        [Test]
        public void TryGetIndex_IsCaseSensitive_BecauseTheGeneratedConstsAre()
        {
            this._registry.Add("Click", "Assets/A.asset");

            Assert.That(this._registry.TryGetIndex("click", out _), Is.False);
        }

        [Test]
        public void Add_ADuplicateId_ThrowsNamingBothOwners()
        {
            this._registry.Add("Click", "Assets/UI/Click.asset");

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => this._registry.Add("Click", "Assets/Menu/Click.asset"));

            Assert.That(exception.Message, Does.Contain("Assets/UI/Click.asset")
                .And.Contain("Assets/Menu/Click.asset"));
        }

        [Test]
        public void Add_AnIdTheFormatRefuses_Throws()
        {
            Assert.Throws<ArgumentException>(() => this._registry.Add("ui click", "Assets/A.asset"));
        }

        [Test]
        public void Add_AnEmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() => this._registry.Add(string.Empty, "Assets/A.asset"));
        }

        [Test]
        public void Count_TracksWhatHasBeenRegistered()
        {
            Assert.That(this._registry.Count, Is.EqualTo(0));

            this._registry.Add("Click", "Assets/A.asset");
            this._registry.Add("Boom", "Assets/B.asset");

            Assert.That(this._registry.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetId_ReturnsWhatWasRegisteredAtThatIndex()
        {
            int index = this._registry.Add("Click", "Assets/A.asset");

            Assert.That(this._registry.GetId(index), Is.EqualTo("Click"));
        }

        [Test]
        public void GetOwner_ReturnsWhoRegisteredThatIndex()
        {
            int index = this._registry.Add("Click", "Assets/A.asset");

            Assert.That(this._registry.GetOwner(index), Is.EqualTo("Assets/A.asset"));
        }

        [Test]
        public void GetId_AnIndexThatWasNeverIssued_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => this._registry.GetId(0));
        }

        [Test]
        public void Clear_EmptiesTheTableAndRestartsIndexing()
        {
            this._registry.Add("Click", "Assets/A.asset");
            this._registry.Clear();

            Assert.That(this._registry.Count, Is.EqualTo(0));
            Assert.That(this._registry.TryGetIndex("Click", out _), Is.False);
            Assert.That(this._registry.Add("Boom", "Assets/B.asset"), Is.EqualTo(0));
        }
    }
}
