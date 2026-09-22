using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects how an authored value and an optional random range combine.
    /// </summary>
    /// <remarks>
    /// A range that has not been filled in must leave the authored value completely alone. If an
    /// empty range quietly meant "0 to 0", every entry a designer had not touched would go silent,
    /// which is the kind of default that ships.
    /// </remarks>
    [TestFixture]
    public sealed class AudioValueRangeTests
    {
        [Test]
        public void Resolve_AnEmptyRange_KeepsTheAuthoredValue()
        {
            Assert.That(AudioValueRange.Resolve(authored: 0.8f, min: 0f, max: 0f, roll01: 0.5f),
                Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void Resolve_AZeroWidthRange_KeepsTheAuthoredValue()
        {
            Assert.That(AudioValueRange.Resolve(0.8f, min: 0.3f, max: 0.3f, roll01: 0.5f),
                Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void Resolve_ARealRange_InterpolatesAcrossIt()
        {
            Assert.That(AudioValueRange.Resolve(0.8f, min: 0.4f, max: 0.6f, roll01: 0f),
                Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(AudioValueRange.Resolve(0.8f, min: 0.4f, max: 0.6f, roll01: 1f),
                Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(AudioValueRange.Resolve(0.8f, min: 0.4f, max: 0.6f, roll01: 0.5f),
                Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Resolve_ARangeEnteredBackwards_StillWorks()
        {
            Assert.That(AudioValueRange.Resolve(0.8f, min: 0.6f, max: 0.4f, roll01: 0f),
                Is.EqualTo(0.4f).Within(1e-5f),
                "A designer dragging the handles past each other should not silence the sound.");
        }

        [Test]
        public void Resolve_ARollOutsideZeroToOne_IsClamped()
        {
            Assert.That(AudioValueRange.Resolve(0.8f, 0.4f, 0.6f, roll01: -1f), Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(AudioValueRange.Resolve(0.8f, 0.4f, 0.6f, roll01: 2f), Is.EqualTo(0.6f).Within(1e-5f));
        }
    }
}
