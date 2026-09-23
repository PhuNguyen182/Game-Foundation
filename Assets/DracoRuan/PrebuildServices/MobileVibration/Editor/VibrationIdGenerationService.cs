using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>What a generation would do, worked out without touching disk.</summary>
    public sealed class VibrationIdGenerationPlan
    {
        public string TargetPath;
        public string RenderedText;
        public string ExistingText;

        /// <summary>The rendered text already matches the file, so nothing needs writing.</summary>
        public bool IsNoOp;

        public List<string> AddedIds = new List<string>();

        /// <summary>
        /// Ids that will disappear. Every call site using them stops compiling, which is the point.
        /// </summary>
        public List<string> RemovedIds = new List<string>();

        public IReadOnlyList<VibrationIdConflict> Conflicts = Array.Empty<VibrationIdConflict>();

        /// <summary>Non-null means Apply is refused.</summary>
        public string BlockingError;

        /// <summary>The file's modification time when the plan was built, for a staleness check.</summary>
        public DateTime PlannedWriteTimeUtc;

        public bool CanApply => this.BlockingError == null;
    }

    /// <summary>
    /// Scans the project and writes the generated <c>VibrationId</c> file.
    /// </summary>
    /// <remarks>
    /// <para><b>Always a full rebuild from disk, never an append.</b> Appending per entry would leave
    /// a dead constant behind when an asset is deleted, and that is the worst possible failure: the
    /// call site keeps compiling and only misses at runtime — exactly what the generated class exists
    /// to prevent. Mirrors <c>AudioIdGenerationService</c>.</para>
    ///
    /// <para>Disk is the source of truth rather than the <c>VibrationCollection</c>, because the
    /// collection is a curated list that drifts. An entry created and never registered would
    /// otherwise have no constant, silently.</para>
    ///
    /// <para>There is no <c>VibrationConfig</c> to point the output somewhere else — that asset was
    /// deliberately never created — so the target folder is always <see cref="DefaultFolder"/>.</para>
    /// </remarks>
    public static class VibrationIdGenerationService
    {
        private const string Namespace = "DracoRuan.PrebuildServices.MobileVibration";
        private const string FileName = "VibrationId.cs";
        private const string DefaultFolder = "DracoRuan/PrebuildServices/MobileVibration/Generated";

        /// <summary>Works out what generation would write. Touches nothing.</summary>
        public static VibrationIdGenerationPlan BuildPlan()
        {
            VibrationIdGenerationPlan plan = new VibrationIdGenerationPlan
            {
                TargetPath = ResolveTargetPath(),
                Conflicts = VibrationIdIndex.Conflicts,
            };

            if (plan.Conflicts.Count > 0)
            {
                plan.BlockingError = plan.Conflicts[0].Message;
                return plan;
            }

            string existingAbsolute = ToAbsolutePath(plan.TargetPath);
            bool exists = File.Exists(existingAbsolute);

            plan.ExistingText = exists ? File.ReadAllText(existingAbsolute) : null;
            plan.PlannedWriteTimeUtc = exists ? File.GetLastWriteTimeUtc(existingAbsolute) : default;

            if (exists && !VibrationIdSource.IsGeneratedFile(plan.ExistingText))
            {
                plan.BlockingError =
                    $"A hand-written file already sits at {plan.TargetPath}. Move it out of the way "
                    + "before generating.";
                return plan;
            }

            string newLine = exists ? VibrationIdSource.DetectNewLine(plan.ExistingText) : "\n";
            plan.RenderedText = VibrationIdSource.ForMembers(Namespace, BuildMembers(), newLine).Render();
            plan.IsNoOp = string.Equals(plan.RenderedText, plan.ExistingText, StringComparison.Ordinal);

            DiffMembers(plan);

            return plan;
        }

        /// <summary>
        /// Writes the file. Returns false, having written nothing, on any failure.
        /// </summary>
        /// <remarks>
        /// The single <c>AssetDatabase.Refresh</c> is the last thing that happens, and nothing may be
        /// assumed to run after it: it can reload the domain out from under this method.
        /// </remarks>
        public static bool Apply(VibrationIdGenerationPlan plan, out string error)
        {
            error = null;

            if (plan == null)
            {
                error = "No plan to apply.";
                return false;
            }

            if (!plan.CanApply)
            {
                error = plan.BlockingError;
                return false;
            }

            if (plan.IsNoOp)
            {
                SessionState.SetString(VibrationEditorState.LastStatusKey, "Vibration ids are already up to date.");
                SessionState.SetBool(VibrationEditorState.IdsStaleKey, false);
                return true;
            }

            string absolute = ToAbsolutePath(plan.TargetPath);

            if (File.Exists(absolute) && File.GetLastWriteTimeUtc(absolute) != plan.PlannedWriteTimeUtc)
            {
                error = "The file changed on disk after this preview was built. Refresh and generate again.";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(absolute);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                // UTF-8 without a BOM, matching every other first-party file in this project.
                File.WriteAllText(absolute, plan.RenderedText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception exception)
            {
                error = $"Could not write {plan.TargetPath}: {exception.Message}";
                return false;
            }

            // Recorded before the refresh, because the domain reload it triggers ends this method.
            SessionState.SetString(VibrationEditorState.LastStatusKey,
                $"Generated {plan.AddedIds.Count} new and removed {plan.RemovedIds.Count} id(s). Recompiling.");
            SessionState.SetBool(VibrationEditorState.IdsStaleKey, false);

            VibrationIdIndex.Invalidate();
            AssetDatabase.Refresh();

            return true;
        }

        /// <summary>The project-relative path the file is written to.</summary>
        public static string ResolveTargetPath() => $"Assets/{DefaultFolder}/{FileName}";

        private static List<VibrationIdMember> BuildMembers()
        {
            List<VibrationIdMember> members = new List<VibrationIdMember>();
            IReadOnlyList<VibrationIdRecord> entries = VibrationIdIndex.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                VibrationIdSanitizeResult sanitized = VibrationIdSanitizer.ToMemberName(entries[i].Id);
                if (sanitized.Status == VibrationIdStatus.Rejected)
                    continue;

                members.Add(new VibrationIdMember(sanitized.MemberName, entries[i].Id, entries[i].OwnerPath));
            }

            return members;
        }

        /// <remarks>
        /// Compared against the constants already in the file rather than against a remembered list,
        /// so the warning is right even if the file was edited or restored behind our back.
        /// </remarks>
        private static void DiffMembers(VibrationIdGenerationPlan plan)
        {
            HashSet<string> before = ExtractValues(plan.ExistingText);
            HashSet<string> after = ExtractValues(plan.RenderedText);

            foreach (string value in after)
            {
                if (!before.Contains(value))
                    plan.AddedIds.Add(value);
            }

            foreach (string value in before)
            {
                if (!after.Contains(value))
                    plan.RemovedIds.Add(value);
            }

            plan.AddedIds.Sort(StringComparer.Ordinal);
            plan.RemovedIds.Sort(StringComparer.Ordinal);
        }

        private static HashSet<string> ExtractValues(string source)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);

            if (string.IsNullOrEmpty(source))
                return values;

            const string marker = "public const string ";
            int cursor = 0;

            while (true)
            {
                int start = source.IndexOf(marker, cursor, StringComparison.Ordinal);
                if (start < 0)
                    break;

                int openQuote = source.IndexOf('"', start);
                int closeQuote = openQuote < 0 ? -1 : source.IndexOf('"', openQuote + 1);

                if (openQuote < 0 || closeQuote < 0)
                    break;

                values.Add(source.Substring(openQuote + 1, closeQuote - openQuote - 1));
                cursor = closeQuote + 1;
            }

            return values;
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    /// <summary>Keys for state that must outlive a domain reload but not the Editor session.</summary>
    public static class VibrationEditorState
    {
        public const string LastStatusKey = "DracoRuan.Vibration.LastStatus";
        public const string IdsStaleKey = "DracoRuan.Vibration.IdsStale";
        public const string SelectedGuidKey = "DracoRuan.Vibration.SelectedGuid";
        public const string LastSaveDirectoryKey = "DracoRuan.Vibration.LastSaveDirectory";
    }
}
