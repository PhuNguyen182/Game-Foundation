using System;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>
    /// Serializes a System.Type by assembly-qualified name. A proper type-picker Inspector
    /// (constrained to UIViewModel subclasses) belongs to the Editor tooling phase; until then
    /// this stores whatever ResolveType() can parse back, and UIRegistry validation catches an
    /// unresolvable reference at service init instead of failing silently at open time.
    /// </summary>
    [Serializable]
    public sealed class SerializableTypeRef
    {
        [SerializeField] private string assemblyQualifiedName;

        public string AssemblyQualifiedName => this.assemblyQualifiedName;

        public Type ResolveType() =>
            string.IsNullOrEmpty(this.assemblyQualifiedName) ? null : Type.GetType(this.assemblyQualifiedName);
    }
}
