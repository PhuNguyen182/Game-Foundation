using DracoRuan.PrebuildServices.MobileVibration.Data;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Processors
{
    /// <summary>
    /// Keeps <see cref="VibrationIdIndex"/> in step with the project.
    /// </summary>
    /// <remarks>
    /// <para><b>It never writes C#.</b> Generating the identifier file here would recompile on every
    /// asset import — a three hundred file art drop, a <c>git pull</c> — and a domain reload
    /// triggered from inside the import pipeline tears that pipeline down while it is still running.
    /// This only marks the cache stale and notes that the ids no longer match, which the window shows
    /// as a highlighted Generate button. Mirrors <c>AudioAssetPostprocessor</c>.</para>
    /// </remarks>
    public sealed class VibrationAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool touched = TouchesVibration(importedAssets)
                           || TouchesVibration(movedAssets)
                           || WasVibration(deletedAssets)
                           || WasVibration(movedFromAssetPaths);

            if (!touched)
                return;

            VibrationIdIndex.Invalidate();
            SessionState.SetBool(VibrationEditorState.IdsStaleKey, true);
        }

        /// <remarks>
        /// Asks the AssetDatabase for the type rather than loading the asset, which is much cheaper
        /// across a large import.
        /// </remarks>
        private static bool TouchesVibration(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                System.Type type = AssetDatabase.GetMainAssetTypeAtPath(paths[i]);

                if (type == typeof(VibrationEntry) || type == typeof(VibrationCollection))
                    return true;
            }

            return false;
        }

        /// <remarks>
        /// A deleted asset cannot be loaded or type-queried, so the only way to notice one is to
        /// check the paths we already knew about. Leaving this out is what leaves a ghost id in every
        /// dropdown until the next domain reload.
        /// </remarks>
        private static bool WasVibration(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (VibrationIdIndex.WasKnownEntryPath(paths[i]))
                    return true;
            }

            return false;
        }
    }
}
