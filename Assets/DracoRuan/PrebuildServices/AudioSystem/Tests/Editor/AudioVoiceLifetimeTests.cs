using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects the time budget the voice pool uses to reclaim a finished one-shot.
    /// </summary>
    /// <remarks>
    /// The pool deliberately does not reclaim on <c>!AudioSource.isPlaying</c>: that reads false for
    /// a frame after <c>PlayDelayed</c>, false while the listener is paused, and false when Unity
    /// virtualises the source under its own voice cap. All three would recycle a voice that is
    /// still meant to be audible, and the caller's handle would start controlling someone else's
    /// sound. A computed end time has none of those failure modes, which is why it is the primary
    /// test and why it is worth pinning here.
    /// </remarks>
    [TestFixture]
    public sealed class AudioVoiceLifetimeTests
    {
        [Test]
        public void CalculateEndTime_AtNormalPitch_IsStartPlusClipLength()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 10f, delaySeconds: 0f, clipLengthSeconds: 2f,
                startOffsetSeconds: 0f, pitch: 1f, isLoop: false);

            Assert.That(end, Is.EqualTo(12f).Within(1e-4f));
        }

        [Test]
        public void CalculateEndTime_AtDoublePitch_TakesHalfTheTime()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 0f, delaySeconds: 0f, clipLengthSeconds: 2f,
                startOffsetSeconds: 0f, pitch: 2f, isLoop: false);

            Assert.That(end, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void CalculateEndTime_AtHalfPitch_TakesTwiceTheTime()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 0f, delaySeconds: 0f, clipLengthSeconds: 2f,
                startOffsetSeconds: 0f, pitch: 0.5f, isLoop: false);

            Assert.That(end, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void CalculateEndTime_WithAStartOffset_OnlyBudgetsTheRemainder()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 0f, delaySeconds: 0f, clipLengthSeconds: 10f,
                startOffsetSeconds: 7f, pitch: 1f, isLoop: false);

            Assert.That(end, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void CalculateEndTime_WithADelay_PushesTheWholeBudgetBack()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 5f, delaySeconds: 1.5f, clipLengthSeconds: 2f,
                startOffsetSeconds: 0f, pitch: 1f, isLoop: false);

            Assert.That(end, Is.EqualTo(8.5f).Within(1e-4f));
        }

        [Test]
        public void CalculateEndTime_ForALoop_NeverExpires()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 0f, delaySeconds: 0f, clipLengthSeconds: 2f,
                startOffsetSeconds: 0f, pitch: 1f, isLoop: true);

            Assert.That(float.IsPositiveInfinity(end), Is.True,
                "A looping voice is only ever ended by an explicit Stop.");
        }

        [Test]
        public void CalculateEndTime_AtZeroPitch_NeverExpires()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 0f, delaySeconds: 0f, clipLengthSeconds: 2f,
                startOffsetSeconds: 0f, pitch: 0f, isLoop: false);

            Assert.That(float.IsPositiveInfinity(end), Is.True,
                "Dividing the remaining length by zero would otherwise produce infinity or NaN by accident.");
        }

        [Test]
        public void CalculateEndTime_AtNegativePitch_NeverExpires()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 0f, delaySeconds: 0f, clipLengthSeconds: 2f,
                startOffsetSeconds: 0f, pitch: -1f, isLoop: false);

            Assert.That(float.IsPositiveInfinity(end), Is.True);
        }

        [Test]
        public void CalculateEndTime_WhenTheOffsetIsPastTheEnd_ExpiresImmediately()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 4f, delaySeconds: 0f, clipLengthSeconds: 2f,
                startOffsetSeconds: 5f, pitch: 1f, isLoop: false);

            Assert.That(end, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void CalculateEndTime_WithNoClip_ExpiresImmediately()
        {
            float end = AudioVoiceLifetime.CalculateEndTime(
                startTime: 3f, delaySeconds: 0f, clipLengthSeconds: 0f,
                startOffsetSeconds: 0f, pitch: 1f, isLoop: false);

            Assert.That(end, Is.EqualTo(3f).Within(1e-4f));
        }
    }
}
