using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor
{
    /// <summary>
    /// Every confirmation this tool shows.
    /// </summary>
    /// <remarks>
    /// Gathered in one place because the wording is the safety net. Nothing here offers undo:
    /// <c>Undo.RegisterCreatedObjectUndo</c> cannot meaningfully cover an asset write plus a C#
    /// file write, so the dialog says what will happen and is the last chance to stop.
    /// </remarks>
    public static class AudioDialogs
    {
        public static bool ConfirmGenerate(AudioIdGenerationPlan plan)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine($"Write {plan.TargetPath}.");
            message.AppendLine();
            message.AppendLine($"Adding {plan.AddedIds.Count} identifier(s), removing {plan.RemovedIds.Count}.");

            if (plan.RemovedIds.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("These identifiers will be removed:");
                message.AppendLine("    " + string.Join(", ", Preview(plan.RemovedIds)));
                message.AppendLine();
                message.AppendLine("Any code using them will stop compiling until it is updated. "
                                   + "That is intended: it points at every call site that needs changing.");
            }

            return EditorUtility.DisplayDialog("Generate audio ids", message.ToString(), "Generate", "Cancel");
        }

        public static bool ConfirmDelete(IReadOnlyList<string> assetPaths, IReadOnlyList<string> removedIds)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine($"Delete {assetPaths.Count} AudioEntry asset(s):");
            message.AppendLine("    " + string.Join(", ", Preview(assetPaths)));
            message.AppendLine();
            message.AppendLine("They will be removed from the project and from the AudioCollection, and "
                               + "their identifiers will disappear from the generated AudioId class.");

            if (removedIds.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Code using " + string.Join(", ", Preview(removedIds))
                                   + " will stop compiling until it is updated.");
            }

            message.AppendLine();
            message.AppendLine("This cannot be undone.");

            return EditorUtility.DisplayDialog("Delete audio entries", message.ToString(), "Delete", "Cancel");
        }

        public static bool ConfirmRename(string oldId, string newId)
        {
            string message = $"Rename '{oldId}' to '{newId}'.\n\n"
                             + $"AudioId.{oldId} will disappear, so any code using it will stop compiling "
                             + "until it is updated.";

            return EditorUtility.DisplayDialog("Rename audio id", message, "Rename", "Cancel");
        }

        public static bool ConfirmSaveAsUnique(string requestedPath, string uniquePath)
        {
            string message = $"{requestedPath} already exists.\n\n"
                             + "Replacing it would give the asset a new GUID and break every reference to "
                             + $"the old one.\n\nSave as {uniquePath} instead?";

            return EditorUtility.DisplayDialog("That file already exists", message, "Save as new", "Cancel");
        }

        public static void Report(string title, string message) =>
            EditorUtility.DisplayDialog(title, message, "OK");

        private static IEnumerable<string> Preview(IReadOnlyList<string> values)
        {
            const int maximum = 12;

            for (int i = 0; i < values.Count && i < maximum; i++)
                yield return values[i];

            if (values.Count > maximum)
                yield return $"and {values.Count - maximum} more";
        }
    }
}
