using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects "pick a different variant than last time".
    /// </summary>
    /// <remarks>
    /// The obvious implementation — roll, and re-roll while it matches — has no bound on how long
    /// it runs and still repeats sometimes. Mapping the roll onto the other variants instead is
    /// exact and constant time, but it is an off-by-one away from either never picking the last
    /// variant or picking the previous one anyway, and both failures are inaudible in testing and
    /// obvious after an hour of play.
    /// </remarks>
    [TestFixture]
    public sealed class AudioClipVariantSelectorTests
    {
        [Test]
        public void Next_WithNoVariants_ReturnsTheFirstIndex()
        {
            Assert.That(AudioClipVariantSelector.Next(variantCount: 0, lastIndex: -1, roll01: 0.5f), Is.EqualTo(0));
        }

        [Test]
        public void Next_WithOneVariant_AlwaysReturnsIt()
        {
            Assert.That(AudioClipVariantSelector.Next(1, lastIndex: 0, roll01: 0.99f), Is.EqualTo(0));
        }

        [Test]
        public void Next_OnTheFirstPlay_CanReturnAnyVariant()
        {
            Assert.That(AudioClipVariantSelector.Next(4, lastIndex: -1, roll01: 0f), Is.EqualTo(0));
            Assert.That(AudioClipVariantSelector.Next(4, lastIndex: -1, roll01: 0.99f), Is.EqualTo(3));
        }

        [Test]
        public void Next_NeverRepeatsTheLastVariant()
        {
            for (int last = 0; last < 5; last++)
            {
                for (int step = 0; step <= 100; step++)
                {
                    int picked = AudioClipVariantSelector.Next(5, last, step / 100f);
                    Assert.That(picked, Is.Not.EqualTo(last), $"last={last} roll={step / 100f}");
                }
            }
        }

        [Test]
        public void Next_StaysInRange()
        {
            for (int last = -1; last < 5; last++)
            {
                for (int step = 0; step <= 100; step++)
                {
                    int picked = AudioClipVariantSelector.Next(5, last, step / 100f);
                    Assert.That(picked, Is.InRange(0, 4), $"last={last} roll={step / 100f}");
                }
            }
        }

        [Test]
        public void Next_CanStillReachEveryOtherVariant()
        {
            bool[] seen = new bool[4];

            for (int step = 0; step <= 100; step++)
                seen[AudioClipVariantSelector.Next(4, lastIndex: 1, roll01: step / 100f)] = true;

            Assert.That(seen[0], Is.True);
            Assert.That(seen[1], Is.False, "That is the one we just played.");
            Assert.That(seen[2], Is.True);
            Assert.That(seen[3], Is.True);
        }

        [Test]
        public void Next_WithARollAtTheUpperBound_DoesNotOverflow()
        {
            Assert.That(AudioClipVariantSelector.Next(3, lastIndex: 0, roll01: 1f), Is.InRange(0, 2));
        }

        [Test]
        public void Next_WithAStaleLastIndex_StillPicksSomethingValid()
        {
            Assert.That(AudioClipVariantSelector.Next(3, lastIndex: 99, roll01: 0.5f), Is.InRange(0, 2));
        }
    }
}
