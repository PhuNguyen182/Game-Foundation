using System;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>Restricts a SerializableTypeRef field's picker (UISystem.Editor's
    /// SerializableTypeRefDrawer) to subclasses of `BaseType`. Purely an Editor hint - ResolveType()
    /// itself does not enforce this at runtime.</summary>
    public sealed class TypeConstraintAttribute : PropertyAttribute
    {
        public readonly Type BaseType;

        public TypeConstraintAttribute(Type baseType) => this.BaseType = baseType;
    }
}
