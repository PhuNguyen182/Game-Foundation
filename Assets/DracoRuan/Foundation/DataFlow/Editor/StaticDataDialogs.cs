using System.Collections.Generic;
using UnityEditor;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Confirmation and result dialogs for the Static Config Data Manager.
    /// </summary>
    /// <remarks>
    /// <para>Shaped like <see cref="LocalDataDialogs"/>: every operation that touches disk asks
    /// first and reports afterwards — including when the user cancels, so "nothing happened" is
    /// never left ambiguous. Batch operations ask once and report once.</para>
    ///
    /// <para>The confirmation here carries more weight than the save-side one, because what is being
    /// overwritten is <b>source code</b> rather than a save file: the dialog shows the chain before
    /// and after, so the change is read before it is made rather than discovered afterwards in a
    /// diff.</para>
    ///
    /// <para>All copy is English, matching the existing tool.</para>
    /// </remarks>
    public static class StaticDataDialogs
    {
        private const int MaxListedItems = 12;

        // -----------------------------------------------------------------
        // Confirmations
        // -----------------------------------------------------------------

        /// <summary>Asks before rewriting one controller's chain, showing the exact before and after.</summary>
        public static bool ConfirmApply(string displayName, string scriptPath, string before, string after) =>
            EditorUtility.DisplayDialog(
                "Apply Chain",
                $"Rewrite the source chain for '{displayName}'?\n\n" +
                $"File:\n   {scriptPath}\n\n" +
                $"Current:\n{Indent(before)}\n\n" +
                $"New:\n{Indent(after)}\n\n" +
                "The file will be saved and Unity will recompile.",
                "Apply", "Cancel");

        /// <summary>Asks before rewriting several controllers at once.</summary>
        public static bool ConfirmApplyAll(IReadOnlyList<string> lines) =>
            EditorUtility.DisplayDialog(
                "Apply All Chains",
                $"Rewrite the source chain for {lines.Count} controller(s)?\n\n" +
                FormatList(lines) +
                "\nEach file will be saved and Unity will recompile once at the end.",
                "Apply All", "Cancel");

        /// <summary>Asks before throwing away edits that were never applied.</summary>
        public static bool ConfirmRevertAll(int count) =>
            EditorUtility.DisplayDialog(
                "Revert Changes",
                $"Discard unapplied chain edits on {count} controller(s)?\n\n" +
                "The source files were never modified, so this only clears the editor.",
                "Revert", "Cancel");

        // -----------------------------------------------------------------
        // Results
        // -----------------------------------------------------------------

        /// <summary>Shown when the user declines. Confirms that nothing was touched.</summary>
        public static void ReportCancelled() =>
            EditorUtility.DisplayDialog("Cancelled", "No files were modified.", "OK");

        public static void ReportApplyFailed(string displayName, string error) =>
            EditorUtility.DisplayDialog(
                "Apply Failed",
                $"Could not rewrite the chain for '{displayName}'.\n\n{error}\n\n" +
                "The source file was left untouched.",
                "OK");

        /// <summary>
        /// One summary for a batch. The title changes with the outcome so a partial failure is not
        /// mistaken for a clean run.
        /// </summary>
        public static void ReportBatch(int succeeded, int total, IReadOnlyList<string> failures)
        {
            bool hasFailures = failures.Count > 0;

            string title = hasFailures ? "Apply All Finished with Errors" : "Apply All Complete";
            string body = $"Rewrote {succeeded} of {total} controller(s).";

            if (hasFailures)
                body += "\n\nFailed:\n" + FormatList(failures);

            EditorUtility.DisplayDialog(title, body, "OK");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Bullets the entries, truncating very long lists so the dialog stays readable rather than
        /// running off the screen.
        /// </summary>
        private static string FormatList(IReadOnlyList<string> items)
        {
            if (items.Count == 0)
                return string.Empty;

            System.Text.StringBuilder builder = new();
            int shown = items.Count > MaxListedItems ? MaxListedItems : items.Count;

            for (int i = 0; i < shown; i++)
                builder.Append("   • ").Append(items[i]).Append('\n');

            if (items.Count > shown)
                builder.Append("   … and ").Append(items.Count - shown).Append(" more\n");

            return builder.ToString();
        }

        /// <summary>
        /// Indents a code block so it reads as a quoted excerpt inside the dialog rather than
        /// running together with the prose around it.
        /// </summary>
        private static string Indent(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "   (none)";

            string[] lines = code.Replace("\r\n", "\n").Split('\n');
            System.Text.StringBuilder builder = new();

            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    builder.Append('\n');

                builder.Append("   ").Append(lines[i].TrimEnd());
            }

            return builder.ToString();
        }
    }
}
