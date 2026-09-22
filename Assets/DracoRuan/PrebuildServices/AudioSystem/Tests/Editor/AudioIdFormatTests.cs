using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects the charset rule for audio identifiers.
    /// </summary>
    /// <remarks>
    /// The rule is the same one <c>DomainId</c> uses for save domains, but for a different reason:
    /// an audio id is emitted as a C# <c>const</c> member name, so anything outside this charset
    /// either will not compile or has to be rewritten behind the author's back. Rejecting at the
    /// text field keeps the generated member name and the id itself identical, which is what makes
    /// a diff of the generated file readable as a list of ids.
    /// </remarks>
    [TestFixture]
    public sealed class AudioIdFormatTests
    {
        [Test]
        public void IsValid_APlainIdentifier_IsAccepted()
        {
            Assert.That(AudioIdFormat.IsValid("ButtonClick", out string error), Is.True);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void IsValid_DigitsUnderscoresAndHyphens_AreAccepted()
        {
            Assert.That(AudioIdFormat.IsValid("sfx_explosion-02", out _), Is.True);
        }

        [Test]
        public void IsValid_Empty_IsRejected()
        {
            Assert.That(AudioIdFormat.IsValid(string.Empty, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void IsValid_Null_IsRejected()
        {
            Assert.That(AudioIdFormat.IsValid(null, out _), Is.False);
        }

        [Test]
        public void IsValid_ExactlyMaxLength_IsAccepted()
        {
            Assert.That(AudioIdFormat.IsValid(new string('a', AudioIdFormat.MaxLength), out _), Is.True);
        }

        [Test]
        public void IsValid_LongerThanMaxLength_IsRejected()
        {
            Assert.That(AudioIdFormat.IsValid(new string('a', AudioIdFormat.MaxLength + 1), out string error), Is.False);
            Assert.That(error, Does.Contain(AudioIdFormat.MaxLength.ToString()));
        }

        [Test]
        public void IsValid_ASpace_IsRejected()
        {
            Assert.That(AudioIdFormat.IsValid("button click", out _), Is.False);
        }

        [Test]
        public void IsValid_ADot_IsRejected()
        {
            Assert.That(AudioIdFormat.IsValid("ui.click", out _), Is.False);
        }

        [Test]
        public void IsValid_VietnameseText_IsRejected_AndTheErrorNamesTheCharacter()
        {
            Assert.That(AudioIdFormat.IsValid("TiengViệt", out string error), Is.False);
            Assert.That(error, Does.Contain("ệ"));
        }
    }
}
