using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>
    /// Every confirmation this tool shows.
    /// </summary>
    /// <remarks>
    /// Gathered in one place because the wording is the safety net, the same reasoning
    /// <c>AudioDialogs</c> follows. Nothing here offers undo: <c>Undo.RegisterCreatedObjectUndo</c>
    /// cannot meaningfully cover an asset write plus a C# file write, so the dialog says what will
    /// happen and is the last chance to stop.
    /// </remarks>
    public static class VibrationDialogs
    {
        public static bool ConfirmGenerate(VibrationIdGenerationPlan plan)
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

            return EditorUtility.DisplayDialog("Generate vibration ids", message.ToString(), "Generate", "Cancel");
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
