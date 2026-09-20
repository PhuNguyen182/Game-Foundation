using System;
using System.IO;
using System.Text;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using NUnit.Framework;

namespace DracoRuan.Foundation.DataFlow.Tests
{
    /// <summary>
    /// Exercises every way an interrupted write can leave the filesystem.
    ///
    /// <para>
    /// These states are the ones real devices produce — app killed from the task switcher, storage
    /// full, battery dies mid-save — and they are impossible to reproduce by hand in the Editor,
    /// which is exactly why they are tested here rather than clicked through. A bug in this state
    /// machine is not a crash; it is a player's save file quietly disappearing.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class AtomicFileStoreTests
    {
        private const string ValidMarker = "VALID:";

        private string _directory;
        private string _target;

        /// <summary>Content is "valid" when it carries the marker, so corruption is easy to stage.</summary>
        private static bool IsValid(string path)
        {
            try
            {
                return File.Exists(path) && File.ReadAllText(path).StartsWith(ValidMarker, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static byte[] Valid(string body) => Encoding.UTF8.GetBytes(ValidMarker + body);

        private AtomicFileStore NewStore(bool withValidation = true) =>
            new(withValidation ? IsValid : null);

        private string TempPath => AtomicFileStore.GetTempPath(this._target);
        private string BackupPath => AtomicFileStore.GetBackupPath(this._target);

        private void Put(string path, string content) => File.WriteAllText(path, content);
        private string Get(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        [SetUp]
        public void SetUp()
        {
            this._directory = Path.Combine(Path.GetTempPath(), "dataflow-atomic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this._directory);
            this._target = Path.Combine(this._directory, "domain.sav");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(this._directory))
                    Directory.Delete(this._directory, recursive: true);
            }
            catch (Exception)
            {
                // A leaked temp directory must never fail a test run.
            }
        }

        // ---------------------------------------------------------------------
        // Writing
        // ---------------------------------------------------------------------

        [Test]
        public void Write_CreatesTarget_WhenNothingExists()
        {
            this.NewStore().Write(this._target, Valid("first"));

            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "first"));
            Assert.That(File.Exists(this.TempPath), Is.False, "staging file must not be left behind");
        }

        [Test]
        public void Write_KeepsPreviousContentAsBackup()
        {
            AtomicFileStore store = this.NewStore();
            store.Write(this._target, Valid("first"));
            store.Write(this._target, Valid("second"));

            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "second"));
            Assert.That(this.Get(this.BackupPath), Is.EqualTo(ValidMarker + "first"));
            Assert.That(File.Exists(this.TempPath), Is.False);
        }

        [Test]
        public void Write_OverwritesStaleTempFromAnInterruptedWrite()
        {
            this.Put(this.TempPath, "garbage from a killed process");

            this.NewStore().Write(this._target, Valid("clean"));

            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "clean"));
            Assert.That(File.Exists(this.TempPath), Is.False);
        }

        [Test]
        public void Write_IsRepeatable()
        {
            AtomicFileStore store = this.NewStore();
            for (int i = 0; i < 5; i++)
                store.Write(this._target, Valid("v" + i));

            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "v4"));
            Assert.That(this.Get(this.BackupPath), Is.EqualTo(ValidMarker + "v3"));
        }

        [Test]
        public void Write_CreatesMissingDirectory()
        {
            string nested = Path.Combine(this._directory, "a", "b", "domain.sav");

            this.NewStore().Write(nested, Valid("x"));

            Assert.That(File.Exists(nested), Is.True);
        }

        // ---------------------------------------------------------------------
        // Recovery - all eight {target, temp, backup} presence combinations
        // ---------------------------------------------------------------------

        [Test]
        public void Recover_000_NothingPresent()
        {
            Assert.That(this.NewStore().Recover(this._target), Is.EqualTo(RecoveryOutcome.NothingToDo));
        }

        [Test]
        public void Recover_100_TargetOnly_IsIntact()
        {
            this.Put(this._target, ValidMarker + "current");

            Assert.That(this.NewStore().Recover(this._target), Is.EqualTo(RecoveryOutcome.TargetIntact));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "current"));
        }

        [Test]
        public void Recover_010_TempOnly_IsPromoted()
        {
            // The swap had moved the target away and died before the temp took its place.
            this.Put(this.TempPath, ValidMarker + "newer");

            Assert.That(this.NewStore().Recover(this._target),
                Is.EqualTo(RecoveryOutcome.CompletedInterruptedSwap));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "newer"));
            Assert.That(File.Exists(this.TempPath), Is.False);
        }

        [Test]
        public void Recover_001_BackupOnly_IsPromoted()
        {
            this.Put(this.BackupPath, ValidMarker + "older");

            Assert.That(this.NewStore().Recover(this._target), Is.EqualTo(RecoveryOutcome.PromotedBackup));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "older"));
            Assert.That(this.Get(this.BackupPath), Is.EqualTo(ValidMarker + "older"),
                "backup is copied, not moved, so a second failure still has something to fall back on");
        }

        [Test]
        public void Recover_110_TargetAndTemp_DiscardsTemp()
        {
            this.Put(this._target, ValidMarker + "current");
            this.Put(this.TempPath, ValidMarker + "abandoned");

            Assert.That(this.NewStore().Recover(this._target),
                Is.EqualTo(RecoveryOutcome.DiscardedStaleTemp));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "current"),
                "a good target wins over a temp file whose swap never started");
            Assert.That(File.Exists(this.TempPath), Is.False);
        }

        [Test]
        public void Recover_101_TargetAndBackup_IsIntact()
        {
            this.Put(this._target, ValidMarker + "current");
            this.Put(this.BackupPath, ValidMarker + "previous");

            Assert.That(this.NewStore().Recover(this._target), Is.EqualTo(RecoveryOutcome.TargetIntact));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "current"));
            Assert.That(this.Get(this.BackupPath), Is.EqualTo(ValidMarker + "previous"));
        }

        /// <summary>
        /// The dangerous one. The target was moved to backup and the process died before the temp
        /// file was promoted. The temp file holds the NEWER content, so preferring the backup here
        /// would silently discard a save the player already completed.
        /// </summary>
        [Test]
        public void Recover_011_TempAndBackup_PrefersTemp()
        {
            this.Put(this.TempPath, ValidMarker + "newer");
            this.Put(this.BackupPath, ValidMarker + "older");

            Assert.That(this.NewStore().Recover(this._target),
                Is.EqualTo(RecoveryOutcome.CompletedInterruptedSwap));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "newer"),
                "the newer completed write must win");
        }

        [Test]
        public void Recover_111_AllThree_KeepsTarget()
        {
            this.Put(this._target, ValidMarker + "current");
            this.Put(this.TempPath, ValidMarker + "abandoned");
            this.Put(this.BackupPath, ValidMarker + "previous");

            Assert.That(this.NewStore().Recover(this._target),
                Is.EqualTo(RecoveryOutcome.DiscardedStaleTemp));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "current"));
            Assert.That(this.Get(this.BackupPath), Is.EqualTo(ValidMarker + "previous"));
        }

        // ---------------------------------------------------------------------
        // Recovery when content is corrupt rather than merely absent
        // ---------------------------------------------------------------------

        [Test]
        public void Recover_CorruptTargetWithGoodBackup_PromotesBackup()
        {
            this.Put(this._target, "CORRUPT");
            this.Put(this.BackupPath, ValidMarker + "previous");

            Assert.That(this.NewStore().Recover(this._target), Is.EqualTo(RecoveryOutcome.PromotedBackup));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "previous"));
        }

        [Test]
        public void Recover_CorruptTargetWithGoodTemp_PrefersTemp()
        {
            this.Put(this._target, "CORRUPT");
            this.Put(this.TempPath, ValidMarker + "newer");
            this.Put(this.BackupPath, ValidMarker + "older");

            Assert.That(this.NewStore().Recover(this._target),
                Is.EqualTo(RecoveryOutcome.CompletedInterruptedSwap));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "newer"));
        }

        [Test]
        public void Recover_EverythingCorrupt_ReportsUnrecoverableAndDeletesNothing()
        {
            this.Put(this._target, "CORRUPT-A");
            this.Put(this.TempPath, "CORRUPT-B");
            this.Put(this.BackupPath, "CORRUPT-C");

            Assert.That(this.NewStore().Recover(this._target), Is.EqualTo(RecoveryOutcome.Unrecoverable));

            // Every byte must survive so a quarantine path or a bug report can still inspect it.
            Assert.That(this.Get(this._target), Is.EqualTo("CORRUPT-A"));
            Assert.That(this.Get(this.TempPath), Is.EqualTo("CORRUPT-B"));
            Assert.That(this.Get(this.BackupPath), Is.EqualTo("CORRUPT-C"));
        }

        [Test]
        public void Recover_IsIdempotent()
        {
            this.Put(this.TempPath, ValidMarker + "newer");
            this.Put(this.BackupPath, ValidMarker + "older");

            AtomicFileStore store = this.NewStore();
            store.Recover(this._target);

            // Re-running after a kill mid-recovery must not change the outcome.
            Assert.That(store.Recover(this._target), Is.EqualTo(RecoveryOutcome.TargetIntact));
            Assert.That(this.Get(this._target), Is.EqualTo(ValidMarker + "newer"));
        }

        // ---------------------------------------------------------------------
        // Reading
        // ---------------------------------------------------------------------

        [Test]
        public void Read_ReturnsNullWhenNothingExists()
        {
            byte[] content = this.NewStore().Read(this._target, out RecoveryOutcome recovery);

            Assert.That(content, Is.Null);
            Assert.That(recovery, Is.EqualTo(RecoveryOutcome.NothingToDo));
        }

        [Test]
        public void Read_RecoversBeforeReturning()
        {
            this.Put(this.BackupPath, ValidMarker + "recovered");

            byte[] content = this.NewStore().Read(this._target, out RecoveryOutcome recovery);

            Assert.That(recovery, Is.EqualTo(RecoveryOutcome.PromotedBackup));
            Assert.That(Encoding.UTF8.GetString(content), Is.EqualTo(ValidMarker + "recovered"));
        }

        [Test]
        public void Read_ReturnsNullButPreservesFilesWhenUnrecoverable()
        {
            this.Put(this._target, "CORRUPT");

            byte[] content = this.NewStore().Read(this._target, out RecoveryOutcome recovery);

            Assert.That(content, Is.Null);
            Assert.That(recovery, Is.EqualTo(RecoveryOutcome.Unrecoverable));
            Assert.That(File.Exists(this._target), Is.True, "caller decides whether to quarantine");
        }

        [Test]
        public void WriteThenRead_RoundTripsBinaryContent()
        {
            byte[] binary = new byte[512];
            new Random(1234).NextBytes(binary);

            AtomicFileStore store = this.NewStore(withValidation: false);
            store.Write(this._target, binary);

            Assert.That(store.Read(this._target, out _), Is.EqualTo(binary));
        }

        // ---------------------------------------------------------------------
        // Deleting
        // ---------------------------------------------------------------------

        [Test]
        public void Delete_RemovesTargetAndBothSidecars()
        {
            this.Put(this._target, ValidMarker + "a");
            this.Put(this.TempPath, ValidMarker + "b");
            this.Put(this.BackupPath, ValidMarker + "c");

            Assert.That(this.NewStore().Delete(this._target), Is.EqualTo(3));
            Assert.That(File.Exists(this._target), Is.False);
            Assert.That(File.Exists(this.TempPath), Is.False);
            Assert.That(File.Exists(this.BackupPath), Is.False);
        }

        [Test]
        public void Delete_IsSafeWhenNothingExists()
        {
            Assert.That(this.NewStore().Delete(this._target), Is.Zero);
        }

        [Test]
        public void Delete_LeavesNoFileThatRecoveryWouldResurrect()
        {
            AtomicFileStore store = this.NewStore();
            store.Write(this._target, Valid("first"));
            store.Write(this._target, Valid("second"));
            store.Delete(this._target);

            // The old DeleteData() bug was deleting a path that saves never wrote, so data appeared
            // to come back from the dead. A backup left behind would reproduce exactly that.
            Assert.That(store.Recover(this._target), Is.EqualTo(RecoveryOutcome.NothingToDo));
        }
    }
}
