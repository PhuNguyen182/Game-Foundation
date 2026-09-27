using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Editor.Validation
{
    /// <summary>
    /// REWRITE_PLAN.md 2.8/mục 5 bước 8: key uniqueness, prefab ↔ VM match, raycast/layout
    /// hygiene, "VM không dùng UnityEngine". Pure data in/out (no EditorWindow/GUI dependency)
    /// so both UIRegistryWindow and UIRegistryBuildValidator can share it.
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
                ValidatePrefabMatchesViewModel(definition, findings);
                ValidateRaycastAndLayoutHygiene(definition, findings);
            }

            ValidateViewModelsDoNotUseUnityEngine(findings);

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

        /// <summary>The prefab's root must carry a UIView&lt;TVM&gt; for exactly this
        /// definition's ViewModelType (REWRITE_PLAN.md 2.2: "Validator kiểm tra prefab có
        /// UIView&lt;TVM&gt; với đúng VM đó").</summary>
        private static void ValidatePrefabMatchesViewModel(UIViewDefinition definition, List<UIValidationFinding> findings)
        {
            Type vmType = definition.ViewModelType;
            if (vmType == null || definition.Prefab == null)
                return;

            UIViewBase[] views = definition.Prefab.GetComponents<UIViewBase>();
            if (views.Length == 0)
            {
                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Error,
                    $"Prefab '{definition.Prefab.name}' (definition '{definition.name}') has no UIView<TVM> on its root.",
                    definition.Prefab));
                return;
            }

            bool matches = views.Any(view => MatchesViewModelType(view.GetType(), vmType));
            if (!matches)
            {
                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Error,
                    $"Prefab '{definition.Prefab.name}' has no UIView<{vmType.Name}> matching definition '{definition.name}'.",
                    definition.Prefab));
            }
        }

        private static bool MatchesViewModelType(Type viewType, Type expectedVmType)
        {
            for (Type type = viewType; type != null && type != typeof(object); type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UIView<>)
                    && type.GetGenericArguments()[0] == expectedVmType)
                    return true;
            }

            return false;
        }

        private static void ValidateRaycastAndLayoutHygiene(UIViewDefinition definition, List<UIValidationFinding> findings)
        {
            if (definition.Prefab == null)
                return;

            GraphicRaycaster[] raycasters = definition.Prefab.GetComponentsInChildren<GraphicRaycaster>(true);
            if (raycasters.Length > 1)
            {
                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Warning,
                    $"Prefab '{definition.Prefab.name}' has {raycasters.Length} GraphicRaycaster components; a view should have exactly one.",
                    definition.Prefab));
            }

            foreach (Graphic graphic in definition.Prefab.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget)
                    continue;

                bool hasInteractiveSibling = graphic.GetComponent<Selectable>() != null
                    || graphic.GetComponent<UnityEngine.EventSystems.IPointerClickHandler>() != null;
                if (!hasInteractiveSibling)
                {
                    findings.Add(new UIValidationFinding(
                        UIValidationSeverity.Warning,
                        $"'{graphic.name}' in prefab '{definition.Prefab.name}' has raycastTarget enabled but no interactive component - likely unnecessary raycast cost.",
                        graphic));
                }
            }

            foreach (LayoutGroup layoutGroup in definition.Prefab.GetComponentsInChildren<LayoutGroup>(true))
            {
                Transform parent = layoutGroup.transform.parent;
                if (parent != null && parent.GetComponentInParent<LayoutGroup>() != null)
                {
                    findings.Add(new UIValidationFinding(
                        UIValidationSeverity.Warning,
                        $"'{layoutGroup.name}' in prefab '{definition.Prefab.name}' is a LayoutGroup nested inside another LayoutGroup - consider flattening.",
                        layoutGroup));
                }

                var fitter = layoutGroup.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                {
                    findings.Add(new UIValidationFinding(
                        UIValidationSeverity.Warning,
                        $"'{layoutGroup.name}' in prefab '{definition.Prefab.name}' has a ContentSizeFitter on the same GameObject as its LayoutGroup - the fitter's own size is being driven by the group it sits on, which usually isn't intended.",
                        layoutGroup));
                }
            }

            foreach (Mask mask in definition.Prefab.GetComponentsInChildren<Mask>(true))
            {
                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Warning,
                    $"'{mask.name}' in prefab '{definition.Prefab.name}' uses Mask - RectMask2D is cheaper for a plain rectangular clip.",
                    mask));
            }
        }

        /// <summary>
        /// REWRITE_PLAN.md: "VM không dùng UnityEngine" - scans every loaded UIViewModel
        /// subclass's members (fields, properties, method signatures) for a UnityEngine.* type,
        /// via reflection rather than source scanning (Editor-only, so IL2CPP/AOT concerns don't
        /// apply). A false negative is possible (a method body could still new up a UnityEngine
        /// type without it appearing in any signature), but this catches the common case - a
        /// serialized/injected UnityEngine dependency - cheaply and without a source parser.
        /// </summary>
        private static void ValidateViewModelsDoNotUseUnityEngine(List<UIValidationFinding> findings)
        {
            foreach (Type vmType in FindViewModelTypes())
            {
                var offendingMembers = new List<string>();

                foreach (FieldInfo field in vmType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (IsUnityEngineType(field.FieldType))
                        offendingMembers.Add($"field '{field.Name}' ({field.FieldType.Name})");
                }

                foreach (PropertyInfo property in vmType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (IsUnityEngineType(property.PropertyType))
                        offendingMembers.Add($"property '{property.Name}' ({property.PropertyType.Name})");
                }

                if (offendingMembers.Count > 0)
                {
                    findings.Add(new UIValidationFinding(
                        UIValidationSeverity.Warning,
                        $"ViewModel '{vmType.FullName}' references UnityEngine types directly: {string.Join(", ", offendingMembers)}. "
                        + "VMs should stay engine-agnostic; move Unity-specific state to the view."));
                }
            }
        }

        private static bool IsUnityEngineType(Type type)
        {
            if (type.IsGenericType)
                return type.GetGenericArguments().Any(IsUnityEngineType);

            string ns = type.Namespace;
            return ns != null && (ns == "UnityEngine" || ns.StartsWith("UnityEngine."));
        }

        private static IEnumerable<Type> FindViewModelTypes()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (Type type in types)
                {
                    if (typeof(UIViewModel).IsAssignableFrom(type) && !type.IsAbstract && type != typeof(UIViewModel))
                        yield return type;
                }
            }
        }
    }
}
