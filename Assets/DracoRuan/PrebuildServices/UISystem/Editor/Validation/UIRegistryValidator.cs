using System;
using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;

namespace DracoRuan.PrebuildServices.UISystem.Editor.DracoRuan.PrebuildServices.UISystem.Editor.Validation
{
    /// <summary>
    /// Registration correctness for one set of definitions: key uniqueness, required fields
    /// (VM/prefab/layer), and prefab ↔ VM match. Every finding is an Error - anything that would
    /// break at runtime. Pure data in/out (no EditorWindow/GUI dependency) so both
    /// UIRegistryWindow and UIRegistryBuildValidator can share it.
    /// </summary>
    public static class UIRegistryValidator
    {
        public static List<UIValidationFinding> Validate(IEnumerable<UIViewDefinition> definitions)
        {
            var findings = new List<UIValidationFinding>();
            var definitionList = definitions.Where(d => d != null).ToList();

            ValidateKeys(definitionList, findings);

            foreach (UIViewDefinition definition in definitionList)
            {
                ValidateRequiredFields(definition, findings);
                ValidatePrefabMatchesViewModel(definition, findings);
            }

            return findings;
        }

        /// <summary>Every definition's ViewModelType must be set, resolvable, and unique across
        /// the collection - REWRITE_PLAN.md: "Key = ViewModel Type".</summary>
        private static void ValidateKeys(List<UIViewDefinition> definitions, List<UIValidationFinding> findings)
        {
            var seen = new Dictionary<Type, UIViewDefinition>();

            foreach (UIViewDefinition definition in definitions)
            {
                Type vmType = definition.ViewModelType;
                if (vmType == null)
                {
                    findings.Add(new UIValidationFinding(
                        UIValidationSeverity.Error,
                        $"'{definition.name}' has no resolvable ViewModelType.", definition));
                    continue;
                }

                if (seen.TryGetValue(vmType, out UIViewDefinition existing))
                {
                    findings.Add(new UIValidationFinding(
                        UIValidationSeverity.Error,
                        $"Duplicate key '{vmType.Name}': both '{existing.name}' and '{definition.name}' claim it.",
                        definition));
                    continue;
                }

                seen[vmType] = definition;
            }
        }

        /// <summary>Prefab (direct or Addressable) and layer are required; UIRegistry throws at
        /// init without them, so surface it at edit time instead.</summary>
        private static void ValidateRequiredFields(UIViewDefinition definition, List<UIValidationFinding> findings)
        {
            if (!definition.HasPrefabSource)
            {
                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Error,
                    $"'{definition.name}' has no prefab assigned.", definition));
            }

            if (definition.Layer == null)
            {
                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Error,
                    $"'{definition.name}' has no layer assigned.", definition));
            }
        }

        /// <summary>The prefab's view must be a UIView&lt;TVM&gt; for exactly this definition's
        /// ViewModelType, and must sit on the prefab root (REWRITE_PLAN.md 2.2). The field is
        /// typed UIViewBase, so "prefab has no view" can no longer be authored.</summary>
        private static void ValidatePrefabMatchesViewModel(UIViewDefinition definition,
            List<UIValidationFinding> findings)
        {
            Type vmType = definition.ViewModelType;
            UIViewBase view = definition.Prefab;
            if (vmType == null || view == null)
                return;

            if (view.transform != view.transform.root)
            {
                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Error,
                    $"'{view.name}' (definition '{definition.name}') is not on its prefab's root; the view component must sit on the root GameObject.",
                    definition));
            }

            if (!MatchesViewModelType(view.GetType(), vmType))
            {
                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Error,
                    $"Prefab '{view.name}' is a {view.GetType().Name}, not a UIView<{vmType.Name}> matching definition '{definition.name}'.",
                    definition));
            }
        }

        internal static bool MatchesViewModelType(Type viewType, Type expectedVmType) =>
            TryGetViewModelType(viewType, out Type vmType) && vmType == expectedVmType;

        /// <summary>The TVM of the UIView&lt;TVM&gt; a view type derives from, if any.</summary>
        internal static bool TryGetViewModelType(Type viewType, out Type viewModelType)
        {
            for (Type type = viewType; type != null && type != typeof(object); type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UIView<>))
                {
                    viewModelType = type.GetGenericArguments()[0];
                    return true;
                }
            }

            viewModelType = null;
            return false;
        }
    }
}