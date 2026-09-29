using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DracoRuan.PrebuildServices.UISystem.Data;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor.Drawers
{
    /// <summary>REWRITE_PLAN.md mục 5 bước 8, "VM type picker": a dropdown of every loaded,
    /// non-abstract subclass of the field's [TypeConstraint] base type (UIViewModel by
    /// convention - see UIViewDefinition), instead of hand-typing an assembly-qualified name.
    /// OnGUI runs on every editor event (layout, repaint, mouse move, resize), so everything
    /// derived from reflection is computed once per base type and cached; the candidate list
    /// itself comes from TypeCache rather than scanning every type of every assembly.</summary>
    [CustomPropertyDrawer(typeof(SerializableTypeRef))]
    public sealed class SerializableTypeRefDrawer : PropertyDrawer
    {
        private sealed class TypeOptions
        {
            public Type[] Candidates;
            public string[] DisplayNames;
        }

        private static readonly Dictionary<Type, TypeOptions> OptionsByBaseType = new();
        private static readonly Dictionary<FieldInfo, Type> BaseTypeByField = new();
        private static readonly Dictionary<string, Type> ResolvedByName = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty nameProperty = property.FindPropertyRelative("assemblyQualifiedName");

            TypeOptions options = GetOptions(this.GetBaseType());
            Type currentType = Resolve(nameProperty.stringValue);
            int currentIndex = currentType == null ? -1 : Array.IndexOf(options.Candidates, currentType);

            EditorGUI.BeginChangeCheck();
            int selectedDisplayIndex = EditorGUI.Popup(position, label.text, currentIndex + 1, options.DisplayNames);
            if (!EditorGUI.EndChangeCheck())
                return;

            nameProperty.stringValue = selectedDisplayIndex == 0
                ? string.Empty
                : options.Candidates[selectedDisplayIndex - 1].AssemblyQualifiedName;
        }

        private Type GetBaseType()
        {
            FieldInfo field = this.fieldInfo;
            if (field == null)
                return typeof(object);

            if (!BaseTypeByField.TryGetValue(field, out Type baseType))
            {
                baseType = field.GetCustomAttribute<TypeConstraintAttribute>()?.BaseType ?? typeof(object);
                BaseTypeByField[field] = baseType;
            }

            return baseType;
        }

        private static Type Resolve(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName))
                return null;

            if (!ResolvedByName.TryGetValue(assemblyQualifiedName, out Type type))
            {
                type = Type.GetType(assemblyQualifiedName);
                ResolvedByName[assemblyQualifiedName] = type;
            }

            return type;
        }

        private static TypeOptions GetOptions(Type baseType)
        {
            if (OptionsByBaseType.TryGetValue(baseType, out TypeOptions options))
                return options;

            Type[] candidates = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(type => !type.IsAbstract)
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .ToArray();

            options = new TypeOptions
            {
                Candidates = candidates,
                DisplayNames = candidates.Select(type => type.Name).Prepend("<None>").ToArray(),
            };

            OptionsByBaseType[baseType] = options;
            return options;
        }
    }
}