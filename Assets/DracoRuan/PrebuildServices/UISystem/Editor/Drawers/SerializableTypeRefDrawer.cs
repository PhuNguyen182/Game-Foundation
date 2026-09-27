using System;
using System.Linq;
using System.Reflection;
using DracoRuan.PrebuildServices.UISystem.Data;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor.Drawers
{
    /// <summary>REWRITE_PLAN.md mục 5 bước 8, "VM type picker": a dropdown of every loaded,
    /// non-abstract subclass of the field's [TypeConstraint] base type (UIViewModel by
    /// convention - see UIViewDefinition), instead of hand-typing an assembly-qualified name.</summary>
    [CustomPropertyDrawer(typeof(SerializableTypeRef))]
    public sealed class SerializableTypeRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty nameProperty = property.FindPropertyRelative("assemblyQualifiedName");

            Type baseType = (this.fieldInfo?.GetCustomAttribute<TypeConstraintAttribute>())?.BaseType ?? typeof(object);
            Type[] candidates = FindConcreteSubclasses(baseType);

            Type currentType = string.IsNullOrEmpty(nameProperty.stringValue) ? null : Type.GetType(nameProperty.stringValue);
            int currentIndex = Array.IndexOf(candidates, currentType);

            string[] displayNames = candidates.Select(t => t.Name).Prepend("<None>").ToArray();
            int selectedDisplayIndex = EditorGUI.Popup(position, label.text, currentIndex + 1, displayNames);

            nameProperty.stringValue = selectedDisplayIndex == 0
                ? string.Empty
                : candidates[selectedDisplayIndex - 1].AssemblyQualifiedName;
        }

        private static Type[] FindConcreteSubclasses(Type baseType)
        {
            var result = new System.Collections.Generic.List<Type>();

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
                    if (baseType.IsAssignableFrom(type) && !type.IsAbstract && type != baseType)
                        result.Add(type);
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result.ToArray();
        }
    }
}
