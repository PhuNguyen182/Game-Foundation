using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Confirmation and result dialogs for the Local Data Manager.
    /// </summary>
    /// <remarks>
    /// <para>Every operation that touches disk asks first, and reports afterwards — including when
    /// the user cancels, so "nothing happened" is never left ambiguous.</para>
    ///
    /// <para>Batch operations ask once and report once. Prompting per entry would mean clicking
    /// through a dialog for every domain in the project, which teaches people to dismiss dialogs
    /// without reading them — exactly the habit a destructive confirmation must not create.</para>
    ///
    /// <para>All copy is English by request.</para>
    /// </remarks>
    public static class LocalDataDialogs
    {
        private const int MaxListedItems = 12;

        // -----------------------------------------------------------------
        // Confirmations
        // -----------------------------------------------------------------

        /// <summary>Asks before overwriting one domain's save file.</summary>
        public static bool ConfirmSave(
            string displayName,
            int targetVersion,
            string fileName,
            long currentSizeBytes,
            string modifiedUtc,
            bool isLatestVersion,
            int latestVersion)
        {
            string body =
                $"Save '{displayName}' as version {targetVersion}?\n\n" +
                "File will be overwritten:\n" +
                $"   {fileName}\n";

            body += currentSizeBytes >= 0
                ? $"Current file: {FormatBytes(currentSizeBytes)}, {modifiedUtc}\n"
                : "This file does not exist yet and will be created.\n";

            // Saving a version you loaded from history must not silently become a fake migration.
            if (!isLatestVersion)
            {
                body +=
                    $"\nNote: you loaded v{targetVersion}, which is not the latest version " +
                    $"(v{latestVersion}). Saving writes back to v{targetVersion} and leaves " +
                    $"v{latestVersion} untouched.";
            }

            return EditorUtility.DisplayDialog("Save Data", body, "Save", "Cancel");
        }

        /// <summary>Asks before overwriting several domains.</summary>
        public static bool ConfirmSaveAll(IReadOnlyList<string> lines, int loadedCount, int totalCount)
        {
            string body =
                $"Save changes to {lines.Count} of {loadedCount} loaded domain(s)?\n" +
                $"({totalCount} domain(s) discovered in total.)\n\n" +
                FormatList(lines) +
                "\nExisting save files will be overwritten.";

            return EditorUtility.DisplayDialog("Save All Data", body, "Save All", "Cancel");
        }

        /// <summary>Asks before deleting every version of one domain.</summary>
        public static bool ConfirmDelete(string displayName, IReadOnlyList<string> fileNames)
        {
            string body =
                $"Delete all saved data for '{displayName}'?\n\n" +
                $"{fileNames.Count} file(s) will be deleted:\n" +
                FormatList(fileNames) +
                "\nThis cannot be undone.";

            return EditorUtility.DisplayDialog("Delete Data", body, "Delete", "Cancel");
        }

        /// <summary>Asks before deleting everything the tool manages.</summary>
        public static bool ConfirmDeleteAll(int domainCount, int fileCount, string folderPath)
        {
            string body =
                "Delete every save file managed by this tool?\n\n" +
                $"{domainCount} domain(s), {fileCount} file(s) total.\n" +
                $"Folder: {folderPath}\n\n" +
                "This cannot be undone.";

            return EditorUtility.DisplayDialog("Delete All Data", body, "Delete All", "Cancel");
        }

        // -----------------------------------------------------------------
        // Results
        // -----------------------------------------------------------------

        /// <summary>Shown when the user declines. Confirms that nothing was touched.</summary>
        public static void ReportCancelled() =>
            EditorUtility.DisplayDialog("Cancelled", "No changes were made.", "OK");

        public static void ReportSaveSucceeded(string displayName, int version, string fileName, long sizeBytes) =>
            EditorUtility.DisplayDialog(
                "Save Complete",
                $"Saved '{displayName}' as version {version}.\n\n" +
                $"File: {fileName}\n" +
                $"Size: {FormatBytes(sizeBytes)}",
                "OK");

        public static void ReportSaveFailed(string displayName, string error) =>
            EditorUtility.DisplayDialog(
                "Save Failed",
                $"Could not save '{displayName}'.\n\n{error}\n\n" +
                "The existing save file was not modified.",
                "OK");

        public static void ReportLoadFailed(string displayName, string error) =>
            EditorUtility.DisplayDialog(
                "Load Failed",
                $"Could not load '{displayName}'.\n\n{error}\n\n" +
                "The file on disk was left untouched.",
                "OK");

        public static void ReportDeleteSucceeded(string displayName, int fileCount) =>
            EditorUtility.DisplayDialog(
                "Delete Complete",
                $"Deleted {fileCount} file(s) for '{displayName}'.",
                "OK");

        public static void ReportDeleteFailed(string displayName, string error) =>
            EditorUtility.DisplayDialog(
                "Delete Failed",
                $"Could not delete data for '{displayName}'.\n\n{error}",
                "OK");

        /// <summary>
        /// One summary for a batch. The title changes with the outcome so a partial failure is not
        /// mistaken for a clean run.
        /// </summary>
        public static void ReportBatch(string operation, int succeeded, int total, IReadOnlyList<string> failures)
        {
            bool hasFailures = failures.Count > 0;

            string title = hasFailures ? $"{operation} Finished with Errors" : $"{operation} Complete";
            string body = $"{operation.Replace(" All", "d")} {succeeded} of {total} domain(s).";

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

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0)
                return "unknown";

            if (bytes < 1024)
                return bytes.ToString("N0", CultureInfo.InvariantCulture) + " bytes";

            return (bytes / 1024d).ToString("N1", CultureInfo.InvariantCulture) + " KB";
        }
    }
}
