using System;
using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data;

namespace DracoRuan.PrebuildServices.UISystem.Editor.DracoRuan.PrebuildServices.UISystem.Editor.Validation
{
    /// <summary>Cross-collection registration checks that UIRegistryValidator (one definition
    /// set at a time) cannot see: the same view model registered in several collections, and
    /// definitions that no collection references at all.</summary>
    public static class UIRegistryCompletenessChecker
    {
        public static List<UIValidationFinding> Check(
            IReadOnlyList<UIViewCollection> collections, IEnumerable<UIViewDefinition> allDefinitions)
        {
            var findings = new List<UIValidationFinding>();

            CheckDuplicateKeysAcrossCollections(collections, findings);
            CheckOrphans(collections, allDefinitions, findings);

            return findings;
        }

        private static void CheckDuplicateKeysAcrossCollections(
            IReadOnlyList<UIViewCollection> collections, List<UIValidationFinding> findings)
        {
            var owners = new Dictionary<Type, List<(UIViewCollection Collection, UIViewDefinition Definition)>>();

            foreach (UIViewCollection collection in collections)
            {
                foreach (UIViewDefinition definition in collection.Definitions.Where(d => d != null))
                {
                    Type vmType = definition.ViewModelType;
                    if (vmType == null)
                        continue;

                    if (!owners.TryGetValue(vmType, out var list))
                        owners[vmType] = list = new List<(UIViewCollection, UIViewDefinition)>();

                    list.Add((collection, definition));
                }
            }

            foreach (var pair in owners)
            {
                if (pair.Value.Select(o => o.Collection).Distinct().Count() < 2)
                    continue;

                string where = string.Join(", ", pair.Value.Select(o => $"{o.Collection.name}/{o.Definition.name}"));
                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Error,
                    $"Key '{pair.Key.Name}' is registered in more than one collection: {where}.",
                    pair.Value[0].Definition));
            }
        }

        private static void CheckOrphans(
            IReadOnlyList<UIViewCollection> collections, IEnumerable<UIViewDefinition> allDefinitions,
            List<UIValidationFinding> findings)
        {
            var registered = new HashSet<UIViewDefinition>(
                collections.SelectMany(c => c.Definitions).Where(d => d != null));

            foreach (UIViewDefinition definition in allDefinitions)
            {
                if (definition == null || registered.Contains(definition))
                    continue;

                findings.Add(new UIValidationFinding(
                    UIValidationSeverity.Error,
                    $"'{definition.name}' is not registered in any collection.", definition));
            }
        }
    }
}
