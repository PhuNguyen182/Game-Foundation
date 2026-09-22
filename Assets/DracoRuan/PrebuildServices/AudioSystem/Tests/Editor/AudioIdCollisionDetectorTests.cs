using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects the single place that decides whether two audio ids can coexist.
    /// </summary>
    /// <remarks>
    /// Two ids clash in more ways than string equality catches. They can be equal ignoring case,
    /// which is a typo in every real project and would emit two consts differing only by case; or
    /// they can be genuinely different and still generate the same C# member, which is what happens
    /// to <c>a-b</c> and <c>a_b</c>. All of them block generation, because the alternative is a
    /// generated file that either does not compile or compiles into something nobody can read.
    /// </remarks>
    [TestFixture]
    public sealed class AudioIdCollisionDetectorTests
    {
        private static AudioIdRecord Record(string id, string path) => new AudioIdRecord(id, path);

        private static IReadOnlyList<AudioIdRecord> Records(params AudioIdRecord[] records) => records;

        [Test]
        public void Detect_DistinctIds_FindsNothing()
        {
            IReadOnlyList<AudioIdConflict> conflicts = AudioIdCollisionDetector.Detect(Records(
                Record("ButtonClick", "Assets/A.asset"),
                Record("Explosion", "Assets/B.asset")));

            Assert.That(conflicts, Is.Empty);
        }

        [Test]
        public void Detect_TheSameIdTwice_IsFlaggedAndNamesBothOwners()
        {
            IReadOnlyList<AudioIdConflict> conflicts = AudioIdCollisionDetector.Detect(Records(
                Record("ButtonClick", "Assets/UI/ButtonClick.asset"),
                Record("ButtonClick", "Assets/Menu/Click.asset")));

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].Kind, Is.EqualTo(AudioIdConflictKind.DuplicateExact));
            Assert.That(conflicts[0].Message, Does.Contain("Assets/UI/ButtonClick.asset")
                .And.Contain("Assets/Menu/Click.asset"));
            Assert.That(conflicts[0].OwnerPaths, Has.Count.EqualTo(2));
        }

        [Test]
        public void Detect_IdsDifferingOnlyByCase_AreFlagged()
        {
            IReadOnlyList<AudioIdConflict> conflicts = AudioIdCollisionDetector.Detect(Records(
                Record("ButtonClick", "Assets/A.asset"),
                Record("buttonclick", "Assets/B.asset")));

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].Kind, Is.EqualTo(AudioIdConflictKind.DuplicateIgnoringCase));
        }

        [Test]
        public void Detect_DistinctIdsThatGenerateTheSameMember_AreFlagged()
        {
            IReadOnlyList<AudioIdConflict> conflicts = AudioIdCollisionDetector.Detect(Records(
                Record("button-click", "Assets/A.asset"),
                Record("button_click", "Assets/B.asset")));

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].Kind, Is.EqualTo(AudioIdConflictKind.MemberCollision));
            Assert.That(conflicts[0].Message, Does.Contain("button_click"));
        }

        [Test]
        public void Detect_MembersDifferingOnlyByCase_AreFlagged()
        {
            IReadOnlyList<AudioIdConflict> conflicts = AudioIdCollisionDetector.Detect(Records(
                Record("a-b", "Assets/A.asset"),
                Record("A_B", "Assets/B.asset")));

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].Kind, Is.EqualTo(AudioIdConflictKind.MemberCollisionIgnoringCase));
        }

        [Test]
        public void Detect_AnIdTheSanitizerRefuses_IsFlaggedWithItsReason()
        {
            IReadOnlyList<AudioIdConflict> conflicts = AudioIdCollisionDetector.Detect(Records(
                Record("ui click", "Assets/A.asset")));

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].Kind, Is.EqualTo(AudioIdConflictKind.Unusable));
            Assert.That(conflicts[0].Message, Does.Contain("ui click"));
        }

        [Test]
        public void Detect_ReportsEveryConflict_NotJustTheFirst()
        {
            IReadOnlyList<AudioIdConflict> conflicts = AudioIdCollisionDetector.Detect(Records(
                Record("Click", "Assets/A.asset"),
                Record("Click", "Assets/B.asset"),
                Record("Boom", "Assets/C.asset"),
                Record("Boom", "Assets/D.asset")));

            Assert.That(conflicts, Has.Count.EqualTo(2));
        }

        [Test]
        public void Detect_IsDeterministic_SoTheReportDoesNotReorderBetweenRuns()
        {
            AudioIdRecord[] forwards =
            {
                Record("Zulu", "Assets/Z1.asset"), Record("Zulu", "Assets/Z2.asset"),
                Record("Alpha", "Assets/A1.asset"), Record("Alpha", "Assets/A2.asset"),
            };
            AudioIdRecord[] backwards = forwards.Reverse().ToArray();

            string[] first = AudioIdCollisionDetector.Detect(forwards).Select(c => c.Message).ToArray();
            string[] second = AudioIdCollisionDetector.Detect(backwards).Select(c => c.Message).ToArray();

            Assert.That(first.Length, Is.EqualTo(second.Length));
            Assert.That(first.Select(m => m.Split('\'')[1]), Is.EqualTo(second.Select(m => m.Split('\'')[1])));
        }

        [Test]
        public void Detect_NoRecords_FindsNothing()
        {
            Assert.That(AudioIdCollisionDetector.Detect(Records()), Is.Empty);
        }

        // ---- The single-id check behind the text field ----

        [Test]
        public void CheckAgainst_AnUnusedId_IsAvailable()
        {
            AudioIdConflict? conflict = AudioIdCollisionDetector.CheckAgainst(
                "Whoosh", Records(Record("Click", "Assets/A.asset")), ignoreOwnerPath: null);

            Assert.That(conflict.HasValue, Is.False);
        }

        [Test]
        public void CheckAgainst_AnIdAnotherAssetAlreadyHas_IsRefusedAndNamesThatAsset()
        {
            AudioIdConflict? conflict = AudioIdCollisionDetector.CheckAgainst(
                "Click", Records(Record("Click", "Assets/A.asset")), ignoreOwnerPath: null);

            Assert.That(conflict.HasValue, Is.True);
            Assert.That(conflict.Value.Message, Does.Contain("Assets/A.asset"));
        }

        [Test]
        public void CheckAgainst_TheEntryEditingItsOwnId_DoesNotReportItself()
        {
            AudioIdConflict? conflict = AudioIdCollisionDetector.CheckAgainst(
                "Click", Records(Record("Click", "Assets/A.asset")), ignoreOwnerPath: "Assets/A.asset");

            Assert.That(conflict.HasValue, Is.False);
        }

        [Test]
        public void CheckAgainst_AnIdTheSanitizerRefuses_IsRefused()
        {
            AudioIdConflict? conflict = AudioIdCollisionDetector.CheckAgainst(
                "ui click", Records(), ignoreOwnerPath: null);

            Assert.That(conflict.HasValue, Is.True);
            Assert.That(conflict.Value.Kind, Is.EqualTo(AudioIdConflictKind.Unusable));
        }

        [Test]
        public void CheckAgainst_AnIdCollidingOnlyAfterSanitising_IsRefused()
        {
            AudioIdConflict? conflict = AudioIdCollisionDetector.CheckAgainst(
                "button-click", Records(Record("button_click", "Assets/A.asset")), ignoreOwnerPath: null);

            Assert.That(conflict.HasValue, Is.True);
            Assert.That(conflict.Value.Kind, Is.EqualTo(AudioIdConflictKind.MemberCollision));
        }
    }
}
