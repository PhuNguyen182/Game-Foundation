using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects the mapping from an audio id to the C# member name generated for it.
    /// </summary>
    /// <remarks>
    /// <para>The mapping is deliberately almost the identity. Two ids that a human reads as
    /// different must never collapse into one member, so the only adjustments allowed are the two
    /// the charset permits and C# does not: a leading digit and a reserved keyword. In particular
    /// casing is never touched — re-casing <c>button_click</c> to <c>ButtonClick</c> is the fastest
    /// possible way to manufacture a collision with an id that was already <c>ButtonClick</c>.</para>
    ///
    /// <para><see cref="AudioIdSanitizer.Suggest"/> is the one place transliteration happens, and
    /// its output is only ever offered to the author, never applied silently.</para>
    /// </remarks>
    [TestFixture]
    public sealed class AudioIdSanitizerTests
    {
        // ---- Straight through ----

        [Test]
        public void ToMemberName_APlainIdentifier_IsUsedVerbatim()
        {
            AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName("ButtonClick");

            Assert.That(result.Status, Is.EqualTo(AudioIdStatus.Ok));
            Assert.That(result.MemberName, Is.EqualTo("ButtonClick"));
        }

        [Test]
        public void ToMemberName_LowerSnakeCase_KeepsItsCasing()
        {
            AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName("button_click");

            Assert.That(result.Status, Is.EqualTo(AudioIdStatus.Ok));
            Assert.That(result.MemberName, Is.EqualTo("button_click"),
                "Re-casing would collide with an id that is already ButtonClick.");
        }

        // ---- The two adjustments C# forces ----

        [Test]
        public void ToMemberName_ALeadingDigit_GetsAnUnderscorePrefix()
        {
            AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName("3DBoom");

            Assert.That(result.Status, Is.EqualTo(AudioIdStatus.Adjusted));
            Assert.That(result.MemberName, Is.EqualTo("_3DBoom"));
        }

        [Test]
        public void ToMemberName_AHyphen_BecomesAnUnderscore()
        {
            AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName("button-click");

            Assert.That(result.Status, Is.EqualTo(AudioIdStatus.Adjusted));
            Assert.That(result.MemberName, Is.EqualTo("button_click"));
        }

        [Test]
        public void ToMemberName_AReservedKeyword_BecomesAVerbatimIdentifier()
        {
            AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName("new");

            Assert.That(result.Status, Is.EqualTo(AudioIdStatus.Adjusted));
            Assert.That(result.MemberName, Is.EqualTo("@new"),
                "A verbatim identifier compiles and leaves the emitted value untouched.");
        }

        [Test]
        public void ToMemberName_AContextualKeyword_IsLeftAlone()
        {
            Assert.That(AudioIdSanitizer.ToMemberName("value").MemberName, Is.EqualTo("value"));
            Assert.That(AudioIdSanitizer.ToMemberName("record").MemberName, Is.EqualTo("record"));
        }

        [Test]
        public void ToMemberName_WhenTheMemberMatchesTheId_ReportsOkRatherThanAdjusted()
        {
            Assert.That(AudioIdSanitizer.ToMemberName("Explosion").Status, Is.EqualTo(AudioIdStatus.Ok));
        }

        // ---- Refusals ----

        [Test]
        public void ToMemberName_AnEmptyId_IsRejected()
        {
            AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName(string.Empty);

            Assert.That(result.Status, Is.EqualTo(AudioIdStatus.Rejected));
            Assert.That(result.MemberName, Is.Null);
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ToMemberName_AnOverlongId_IsRejectedRatherThanTruncated()
        {
            AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName(new string('a', AudioIdFormat.MaxLength + 1));

            Assert.That(result.Status, Is.EqualTo(AudioIdStatus.Rejected),
                "Truncating would manufacture collisions between two long ids sharing a prefix.");
        }

        [Test]
        public void ToMemberName_NonAsciiCharacters_AreRejectedWithAnAsciiSuggestion()
        {
            AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName("Tiếng Việt");

            Assert.That(result.Status, Is.EqualTo(AudioIdStatus.Rejected));
            Assert.That(result.Message, Does.Contain("Tieng_Viet"));
        }

        // ---- Suggestions ----

        [Test]
        public void Suggest_StripsVietnameseDiacritics()
        {
            Assert.That(AudioIdSanitizer.Suggest("Tiếng Việt"), Is.EqualTo("Tieng_Viet"));
        }

        [Test]
        public void Suggest_MapsTheStrokedD_BecauseNormalisationDoesNotDecomposeIt()
        {
            Assert.That(AudioIdSanitizer.Suggest("Đàn"), Is.EqualTo("Dan"));
            Assert.That(AudioIdSanitizer.Suggest("đàn"), Is.EqualTo("dan"));
        }

        [Test]
        public void Suggest_CollapsesARunOfIllegalCharactersIntoOneUnderscore()
        {
            Assert.That(AudioIdSanitizer.Suggest("ui   click!!!now"), Is.EqualTo("ui_click_now"));
        }

        [Test]
        public void Suggest_TrimsLeadingAndTrailingUnderscores()
        {
            Assert.That(AudioIdSanitizer.Suggest("  click  "), Is.EqualTo("click"));
        }

        [Test]
        public void Suggest_KeepsAnAlreadyValidIdUnchanged()
        {
            Assert.That(AudioIdSanitizer.Suggest("button-click_02"), Is.EqualTo("button-click_02"));
        }

        [Test]
        public void Suggest_WhenNothingUsableSurvives_ReturnsEmpty()
        {
            Assert.That(AudioIdSanitizer.Suggest("###"), Is.Empty);
        }

        [Test]
        public void Suggest_Null_ReturnsEmpty()
        {
            Assert.That(AudioIdSanitizer.Suggest(null), Is.Empty);
        }
    }
}
