using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.Data;

namespace DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// Type -> definition map, validated once at construction so a bad asset config fails
    /// loudly at init instead of silently at open time. One registry for the root service, and
    /// one more per UIScope.
    /// </summary>
    public sealed class UIRegistry
    {
        private readonly Dictionary<Type, UIViewDefinition> _definitions = new();

        public UIRegistry(IEnumerable<UIViewDefinition> definitions)
        {
            foreach (UIViewDefinition definition in definitions)
            {
                if (!definition)
                    throw new InvalidOperationException("UIRegistry received a null UIViewDefinition entry.");

                Type vmType = definition.ViewModelType;
                if (vmType == null)
                {
                    throw new InvalidOperationException(
                        $"UIViewDefinition '{definition.name}' has an unresolvable ViewModelType (stale or empty type reference).");
                }

                if (!definition.HasPrefabSource)
                    throw new InvalidOperationException($"UIViewDefinition '{definition.name}' (VM: {vmType.Name}) has no prefab assigned.");

                if (!definition.Layer)
                    throw new InvalidOperationException($"UIViewDefinition '{definition.name}' (VM: {vmType.Name}) has no layer assigned.");

                if (!this._definitions.TryAdd(vmType, definition))
                    throw new InvalidOperationException($"Duplicate UIViewDefinition for view model type '{vmType.Name}'.");
            }
        }

        public bool TryGet(Type viewModelType, out UIViewDefinition definition) =>
            this._definitions.TryGetValue(viewModelType, out definition);

        public bool Contains(Type viewModelType) => this._definitions.ContainsKey(viewModelType);

        public IEnumerable<UIViewDefinition> All => this._definitions.Values;
    }
}
