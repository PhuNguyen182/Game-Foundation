using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects the linear/decibel conversion the mixer depends on.
    /// </summary>
    /// <remarks>
    /// The floor at silence is the reason this class exists at all. <c>Log10(0)</c> is
    /// <c>-Infinity</c>, and <c>AudioMixer.SetFloat</c> given a non-finite value poisons the mixer
    /// with NaN until the Editor is restarted — a failure that looks like a hardware problem and
    /// costs an afternoon. Every test below that touches zero is guarding that one bug.
    /// </remarks>
    [TestFixture]
    public sealed class AudioDecibelsTests
    {
        // ---- Linear to decibels ----

        [Test]
        public void LinearToDecibels_FullScale_IsZeroDecibels()
        {
            Assert.That(AudioDecibels.LinearToDecibels(1f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void LinearToDecibels_Half_IsMinusSixDecibels()
        {
            Assert.That(AudioDecibels.LinearToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(1e-3f));
        }

        [Test]
        public void LinearToDecibels_Zero_IsTheFloorRatherThanNegativeInfinity()
        {
            float decibels = AudioDecibels.LinearToDecibels(0f);

            Assert.That(float.IsNegativeInfinity(decibels), Is.False,
                "Log10(0) is -Infinity, and feeding that to AudioMixer.SetFloat poisons the mixer with NaN.");
            Assert.That(decibels, Is.EqualTo(AudioDecibels.MinDecibels));
        }

        [Test]
        public void LinearToDecibels_BelowTheAudibleFloor_ClampsToTheFloor()
        {
            Assert.That(AudioDecibels.LinearToDecibels(0.00001f), Is.EqualTo(AudioDecibels.MinDecibels));
        }

        [Test]
        public void LinearToDecibels_Negative_ClampsToTheFloor()
        {
            Assert.That(AudioDecibels.LinearToDecibels(-1f), Is.EqualTo(AudioDecibels.MinDecibels));
        }

        // ---- Decibels to linear ----

        [Test]
        public void DecibelsToLinear_ZeroDecibels_IsFullScale()
        {
            Assert.That(AudioDecibels.DecibelsToLinear(0f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void DecibelsToLinear_AtTheFloor_IsExactlySilent()
        {
            Assert.That(AudioDecibels.DecibelsToLinear(AudioDecibels.MinDecibels), Is.EqualTo(0f));
        }

        [Test]
        public void DecibelsToLinear_BelowTheFloor_IsExactlySilent()
        {
            Assert.That(AudioDecibels.DecibelsToLinear(-120f), Is.EqualTo(0f));
        }

        // ---- Round trip ----

        [Test]
        public void RoundTrip_PreservesAudibleVolumes()
        {
            float[] volumes = { 1f, 0.75f, 0.5f, 0.25f, 0.1f, 0.01f };

            foreach (float volume in volumes)
            {
                float roundTripped = AudioDecibels.DecibelsToLinear(AudioDecibels.LinearToDecibels(volume));
                Assert.That(roundTripped, Is.EqualTo(volume).Within(1e-4f), $"round trip of {volume}");
            }
        }
    }
}
