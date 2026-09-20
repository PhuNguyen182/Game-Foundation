using System;
using System.IO;

namespace DracoRuan.Foundation.DataFlow.Core.Storage
{
    /// <summary>
    /// Writes a file so that a process kill at any instant leaves either the old contents or the new
    /// contents on disk, never a half-written mixture.
    ///
    /// <para>
    /// Three physical paths back one logical file:
    /// <c>name.sav</c> (target), <c>name.sav.tmp</c> (staging) and <c>name.sav.bak</c> (previous
    /// good copy). A write stages into the temp file, flushes it, then swaps it into place and
    /// demotes the old target to backup.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para><b>Why not just <see cref="File.Replace(string,string,string)"/>.</b> It is the right
    /// primitive where it works, but it is not portable: on Android it can throw depending on the
    /// underlying filesystem, and it fails outright when the backup would land on a different
    /// volume. <see cref="Write"/> tries it first and falls back to an explicit
    /// delete-backup / move-target-to-backup / move-temp-to-target sequence, whose intermediate
    /// states <see cref="Recover"/> knows how to finish.</para>
    ///
    /// <para><b>Durability is best-effort, and honestly so.</b> <c>FileStream.Flush(true)</c> asks
    /// the OS to flush to the device, but it is a no-op on several mobile backends, and .NET cannot
    /// fsync a <i>directory</i>, so the rename itself may still be lost to a power cut. This class
    /// therefore guarantees <i>consistency</i> — you never read a torn file — not that the very last
    /// write survives a sudden power loss. Ordering the operations so every interruption is
    /// recoverable is what buys the consistency.</para>
    ///
    /// <para><b>Recovery reads the filesystem, never a journal.</b> See <see cref="RecoveryOutcome"/>.</para>
    ///
    /// <para>Deliberately has no Unity dependency so it can be exercised directly against a
    /// temporary directory in tests, including the interrupted-write states that are impossible to
    /// reproduce by hand in the Editor.</para>
    /// </remarks>
    public sealed class AtomicFileStore
    {
        /// <summary>Suffix for the staging file.</summary>
        public const string TempSuffix = ".tmp";

        /// <summary>Suffix for the previous good copy.</summary>
        public const string BackupSuffix = ".bak";

        private readonly Func<string, bool> _isContentValid;

        /// <summary>
        /// Creates a store.
        /// </summary>
        /// <param name="isContentValid">
        /// Optional integrity predicate, given a file path. Recovery uses it to tell "the target
        /// exists" from "the target is readable", which is what lets a corrupt target fall back to
        /// the backup instead of being trusted. Pass <c>null</c> to treat mere existence as valid,
        /// which is only appropriate for content with no integrity check of its own.
        /// </param>
        public AtomicFileStore(Func<string, bool> isContentValid = null)
        {
            this._isContentValid = isContentValid;
        }

        public static string GetTempPath(string targetPath) => targetPath + TempSuffix;

        public static string GetBackupPath(string targetPath) => targetPath + BackupSuffix;

        /// <summary>
        /// Writes <paramref name="content"/> to <paramref name="targetPath"/> atomically, keeping the
        /// previous contents as a backup.
        /// </summary>
        public void Write(string targetPath, ReadOnlySpan<byte> content)
        {
            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("Target path must not be empty.", nameof(targetPath));

            string directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string tempPath = GetTempPath(targetPath);
            string backupPath = GetBackupPath(targetPath);

            WriteStaging(tempPath, content);

            // Nothing to replace - a plain move is already atomic.
            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
                return;
            }

            try
            {
                File.Replace(tempPath, targetPath, backupPath, ignoreMetadataErrors: true);
            }
            catch (Exception)
            {
                // Any failure falls back, because File.Replace throws a different exception type per
                // platform and filesystem and a match list would be wrong somewhere.
                FallbackSwap(targetPath, tempPath, backupPath);
            }
        }

        /// <summary>
        /// Performs the target/backup/temp swap without <see cref="File.Replace(string,string,string)"/>.
        /// </summary>
        /// <remarks>
        /// State is re-derived from disk rather than assumed, because <c>File.Replace</c> can fail
        /// <i>after</i> partially completing. Blindly running the full sequence would, in the case
        /// where Replace had already moved the target aside, delete the backup that is by then the
        /// only surviving copy — turning a recoverable failure into permanent data loss.
        /// </remarks>
        private static void FallbackSwap(string targetPath, string tempPath, string backupPath)
        {
            // Temp consumed => Replace actually succeeded and failed only on metadata. Nothing to do.
            if (!File.Exists(tempPath))
                return;

            // Target still present => Replace did not get as far as moving it. Demote it first.
            // Each step leaves a state Recover() can finish:
            //   target + temp            -> DiscardedStaleTemp, or this runs again
            //   backup + temp, no target -> CompletedInterruptedSwap
            //   target + backup          -> TargetIntact
            if (File.Exists(targetPath))
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                File.Move(targetPath, backupPath);
            }

            File.Move(tempPath, targetPath);
        }

        /// <summary>
        /// Reads <paramref name="targetPath"/>, transparently recovering from an interrupted write
        /// first. Returns <c>null</c> when nothing readable exists.
        /// </summary>
        public byte[] Read(string targetPath, out RecoveryOutcome recovery)
        {
            recovery = this.Recover(targetPath);

            return recovery switch
            {
                RecoveryOutcome.NothingToDo => null,
                RecoveryOutcome.Unrecoverable => null,
                _ => File.Exists(targetPath) ? File.ReadAllBytes(targetPath) : null
            };
        }

        /// <summary>
        /// Brings the three physical files back to a consistent state after an interrupted write.
        /// Safe to call repeatedly; every branch is idempotent.
        /// </summary>
        public RecoveryOutcome Recover(string targetPath)
        {
            string tempPath = GetTempPath(targetPath);
            string backupPath = GetBackupPath(targetPath);

            bool hasTarget = File.Exists(targetPath);
            bool hasTemp = File.Exists(tempPath);
            bool hasBackup = File.Exists(backupPath);

            if (!hasTarget && !hasTemp && !hasBackup)
                return RecoveryOutcome.NothingToDo;

            if (hasTarget && this.IsValid(targetPath))
            {
                // A temp file alongside a good target is the debris of a write that never
                // completed its swap. The target is still the current content.
                if (hasTemp)
                {
                    TryDelete(tempPath);
                    return RecoveryOutcome.DiscardedStaleTemp;
                }

                return RecoveryOutcome.TargetIntact;
            }

            // No usable target. The temp file is the NEWER content, so it wins over the backup -
            // preferring the backup here would silently discard a completed save.
            if (hasTemp && this.IsValid(tempPath))
            {
                if (hasTarget)
                    TryDelete(targetPath);

                File.Move(tempPath, targetPath);
                return RecoveryOutcome.CompletedInterruptedSwap;
            }

            if (hasBackup && this.IsValid(backupPath))
            {
                if (hasTarget)
                    TryDelete(targetPath);

                File.Copy(backupPath, targetPath, overwrite: true);
                TryDelete(tempPath);
                return RecoveryOutcome.PromotedBackup;
            }

            // Something is on disk but none of it reads back. Leave every byte where it is so a
            // human or a quarantine path can still inspect it.
            return RecoveryOutcome.Unrecoverable;
        }

        /// <summary>
        /// Deletes the target and all of its sidecars. Used by "delete save data", not by writes.
        /// </summary>
        public int Delete(string targetPath)
        {
            int deleted = 0;
            deleted += TryDelete(targetPath) ? 1 : 0;
            deleted += TryDelete(GetTempPath(targetPath)) ? 1 : 0;
            deleted += TryDelete(GetBackupPath(targetPath)) ? 1 : 0;
            return deleted;
        }

        private void WriteStaging(string tempPath, ReadOnlySpan<byte> content)
        {
            // FileMode.Create truncates any debris from a previous interrupted write.
            using FileStream stream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: false);

            stream.Write(content);

            // Ask for a device-level flush. A no-op on some mobile backends - see the class remarks.
            stream.Flush(flushToDisk: true);
        }

        private bool IsValid(string path)
        {
            if (this._isContentValid == null)
                return true;

            try
            {
                return this._isContentValid(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryDelete(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                File.Delete(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
