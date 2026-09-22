using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using UnityEditor;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor
{
    /// <summary>What a generation would do, worked out without touching disk.</summary>
    public sealed class AudioIdGenerationPlan
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

        public IReadOnlyList<AudioIdConflict> Conflicts = Array.Empty<AudioIdConflict>();

        /// <summary>Non-null means Apply is refused.</summary>
        public string BlockingError;

        /// <summary>The file's modification time when the plan was built, for a staleness check.</summary>
        public DateTime PlannedWriteTimeUtc;

        public bool CanApply => this.BlockingError == null;
    }

    /// <summary>
    /// Scans the project and writes the generated identifier file.
    /// </summary>
    /// <remarks>
    /// <para><b>Always a full rebuild from disk, never an append.</b> Appending per entry would
    /// leave a dead constant behind when an asset is deleted, and that is the worst possible
    /// failure: the call site keeps compiling and only misses at runtime, which is exactly what the
    /// generated class exists to prevent.</para>
    ///
    /// <para>Disk is the source of truth rather than the <c>AudioCollection</c>, because the
    /// collection is a curated list that drifts. An entry created and never registered would
    /// otherwise have no constant, silently.</para>
    /// </remarks>
    public static class AudioIdGenerationService
    {
        private const string Namespace = "DracoRuan.PrebuildServices.AudioSystem";
        private const string FileName = "AudioId.cs";
        private const string DefaultFolder = "DracoRuan/PrebuildServices/AudioSystem/Generated";

        /// <summary>Works out what generation would write. Touches nothing.</summary>
        public static AudioIdGenerationPlan BuildPlan()
        {
            AudioIdGenerationPlan plan = new AudioIdGenerationPlan
            {
                TargetPath = ResolveTargetPath(),
                Conflicts = AudioIdIndex.Conflicts,
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

            if (exists && !AudioIdSource.IsGeneratedFile(plan.ExistingText))
            {
                plan.BlockingError =
                    $"A hand-written file already sits at {plan.TargetPath}. Move it, or point "
                    + "AudioConfig's generated id folder somewhere else.";
                return plan;
            }

            string newLine = exists ? AudioIdSource.DetectNewLine(plan.ExistingText) : "\n";
            plan.RenderedText = AudioIdSource.ForGroups(Namespace, BuildGroups(), newLine).Render();
            plan.IsNoOp = string.Equals(plan.RenderedText, plan.ExistingText, StringComparison.Ordinal);

            DiffMembers(plan);

            return plan;
        }

        /// <summary>
        /// Writes the file. Returns false, having written nothing, on any failure.
        /// </summary>
        /// <remarks>
        /// The single <c>AssetDatabase.Refresh</c> is the last thing that happens, and nothing may
        /// be assumed to run after it: it can reload the domain out from under this method.
        /// </remarks>
        public static bool Apply(AudioIdGenerationPlan plan, out string error)
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
                SessionState.SetString(AudioEditorState.LastStatusKey, "Audio ids are already up to date.");
                SessionState.SetBool(AudioEditorState.IdsStaleKey, false);
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
            SessionState.SetString(AudioEditorState.LastStatusKey,
                $"Generated {plan.AddedIds.Count} new and removed {plan.RemovedIds.Count} id(s). Recompiling.");
            SessionState.SetBool(AudioEditorState.IdsStaleKey, false);

            AudioIdIndex.Invalidate();
            AssetDatabase.Refresh();

            return true;
        }

        /// <summary>The project-relative path the file is written to.</summary>
        public static string ResolveTargetPath()
        {
            string folder = AudioIdIndex.Config != null && !string.IsNullOrEmpty(AudioIdIndex.Config.GeneratedIdFolder)
                ? AudioIdIndex.Config.GeneratedIdFolder
                : DefaultFolder;

            folder = folder.Replace('\\', '/').Trim('/');

            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) && folder != "Assets")
                folder = "Assets/" + folder;

            return $"{folder}/{FileName}";
        }

        private static List<AudioIdGroup> BuildGroups()
        {
            List<AudioIdMember> ids = new List<AudioIdMember>();
            IReadOnlyList<AudioIdRecord> entries = AudioIdIndex.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                AudioIdSanitizeResult sanitized = AudioIdSanitizer.ToMemberName(entries[i].Id);
                if (sanitized.Status == AudioIdStatus.Rejected)
                    continue;

                ids.Add(new AudioIdMember(sanitized.MemberName, entries[i].Id, entries[i].OwnerPath));
            }

            List<AudioIdMember> channels = new List<AudioIdMember>();
            IReadOnlyList<AudioIdRecord> channelRecords = AudioIdIndex.Channels;

            for (int i = 0; i < channelRecords.Count; i++)
            {
                AudioIdSanitizeResult sanitized = AudioIdSanitizer.ToMemberName(channelRecords[i].Id);
                if (sanitized.Status == AudioIdStatus.Rejected)
                    continue;

                channels.Add(new AudioIdMember(sanitized.MemberName, channelRecords[i].Id, channelRecords[i].OwnerPath));
            }

            return new List<AudioIdGroup>
            {
                new AudioIdGroup("AudioId", "Compile-time names for every AudioEntry asset in the project.", ids),
                new AudioIdGroup("AudioChannelId", "Compile-time names for every channel in the audio config.", channels),
            };
        }

        /// <remarks>
        /// Compared against the constants already in the file rather than against a remembered list,
        /// so the warning is right even if the file was edited or restored behind our back.
        /// </remarks>
        private static void DiffMembers(AudioIdGenerationPlan plan)
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
    public static class AudioEditorState
    {
        public const string LastStatusKey = "DracoRuan.Audio.LastStatus";
        public const string IdsStaleKey = "DracoRuan.Audio.IdsStale";
        public const string SelectedGuidKey = "DracoRuan.Audio.SelectedGuid";
        public const string SplitWidthKey = "DracoRuan.Audio.SplitWidth";
        public const string TabKey = "DracoRuan.Audio.Tab";
        public const string LastSaveDirectoryKey = "DracoRuan.Audio.LastSaveDirectory";
    }
}
