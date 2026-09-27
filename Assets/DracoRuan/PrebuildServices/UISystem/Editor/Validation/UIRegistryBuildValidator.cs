using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.UISystem.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DracoRuan.PrebuildServices.UISystem.Editor.Validation
{
    /// <summary>Fails the build if UIRegistryValidator finds an Error-severity issue in any
    /// UIViewCollection asset in the project - REWRITE_PLAN.md mục 5 bước 8, "build
    /// validator". Warnings never block a build; only Errors (bad key, prefab/VM mismatch)
    /// do, since those are guaranteed-broken-at-runtime, not style preferences.</summary>
    public sealed class UIRegistryBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<UIValidationFinding> errors = FindAllCollections()
                .SelectMany(collection => UIRegistryValidator.Validate(collection.Definitions))
                .Where(f => f.Severity == UIValidationSeverity.Error)
                .ToList();

            if (errors.Count == 0)
                return;

            string message = string.Join("\n", errors.Select(f => f.ToString()));
            throw new BuildFailedException($"UISystem registry validation failed with {errors.Count} error(s):\n{message}");
        }

        private static IEnumerable<UIViewCollection> FindAllCollections()
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(UIViewCollection)}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var collection = AssetDatabase.LoadAssetAtPath<UIViewCollection>(path);
                if (collection != null)
                    yield return collection;
            }
        }
    }
}
