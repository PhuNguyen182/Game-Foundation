using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Drawing
{
    /// <summary>
    /// Draws one platform's pulse array from <c>MOST_HapticFeedback.CustomHapticPattern</c>
    /// (<c>IOS_Haptic[]</c> or <c>Android_Haptic[]</c>) as a <see cref="ReorderableList"/>, one line per
    /// pulse.
    /// </summary>
    /// <remarks>
    /// A <see cref="ReorderableList"/> has a single fixed row height and no per-element foldout, so it
    /// cannot reproduce the nested-fade-group failure this rebuild moves away from. One instance is
    /// cached per platform per drawer, keyed by the backing array's <see cref="SerializedProperty"/>,
    /// since <c>ReorderableList</c> is stateful (it does not tolerate being rebuilt every frame).
    /// </remarks>
    public sealed class VibrationPatternSection
    {
        private const float RowHeight = 20f;
        private const float Padding = 4f;

        private ReorderableList _iosList;
        private ReorderableList _androidList;

        public void DrawIOS(SerializedObject serializedObject, SerializedProperty pulses)
        {
            this._iosList ??= BuildIOSList(serializedObject, pulses);
            this._iosList.serializedProperty = pulses;
            this._iosList.DoLayoutList();
        }

        public void DrawAndroid(SerializedObject serializedObject, SerializedProperty pulses)
        {
            this._androidList ??= BuildAndroidList(serializedObject, pulses);
            this._androidList.serializedProperty = pulses;
            this._androidList.DoLayoutList();
        }

        private static ReorderableList BuildIOSList(SerializedObject serializedObject, SerializedProperty pulses)
        {
            ReorderableList list = new ReorderableList(serializedObject, pulses, true, true, true, true)
            {
                elementHeight = RowHeight + Padding
            };

            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "iOS Pulses (Delay / Type)");

            list.drawElementCallback = (rect, index, _, _) =>
            {
                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
                SerializedProperty delay = element.FindPropertyRelative("Delay");
                SerializedProperty pulseType = element.FindPropertyRelative("PulseType");

                rect.y += Padding * 0.5f;
                rect.height = RowHeight;

                Rect delayRect = new Rect(rect.x, rect.y, rect.width * 0.35f, rect.height);
                Rect typeRect = new Rect(delayRect.xMax + 6f, rect.y, rect.width - delayRect.width - 6f, rect.height);

                delay.floatValue = Mathf.Max(0f, EditorGUI.FloatField(delayRect, delay.floatValue));
                EditorGUI.PropertyField(typeRect, pulseType, GUIContent.none);
            };

            list.onAddCallback = target =>
            {
                int index = target.serializedProperty.arraySize;
                target.serializedProperty.InsertArrayElementAtIndex(index);
                target.serializedProperty.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("Delay").floatValue = 0f;
            };

            return list;
        }

        private static ReorderableList BuildAndroidList(SerializedObject serializedObject, SerializedProperty pulses)
        {
            ReorderableList list = new ReorderableList(serializedObject, pulses, true, true, true, true)
            {
                elementHeight = RowHeight + Padding
            };

            list.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Android Pulses (Delay / Duration / Strength)");

            list.drawElementCallback = (rect, index, _, _) =>
            {
                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
                SerializedProperty delay = element.FindPropertyRelative("Delay");
                SerializedProperty pulseTime = element.FindPropertyRelative("PulseTime");
                SerializedProperty pulseStrength = element.FindPropertyRelative("PulseStrength");

                rect.y += Padding * 0.5f;
                rect.height = RowHeight;

                float columnWidth = (rect.width - 12f) / 3f;
                Rect delayRect = new Rect(rect.x, rect.y, columnWidth, rect.height);
                Rect durationRect = new Rect(delayRect.xMax + 6f, rect.y, columnWidth, rect.height);
                Rect strengthRect = new Rect(durationRect.xMax + 6f, rect.y, columnWidth, rect.height);

                delay.longValue = System.Math.Max(0L, EditorGUI.LongField(delayRect, delay.longValue));
                pulseTime.longValue = System.Math.Max(1L, EditorGUI.LongField(durationRect, pulseTime.longValue));
                pulseStrength.intValue = EditorGUI.IntSlider(strengthRect, pulseStrength.intValue, 0, 255);
            };

            list.onAddCallback = target =>
            {
                int index = target.serializedProperty.arraySize;
                target.serializedProperty.InsertArrayElementAtIndex(index);
                SerializedProperty element = target.serializedProperty.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("Delay").longValue = 0L;
                element.FindPropertyRelative("PulseTime").longValue = 1L;
                element.FindPropertyRelative("PulseStrength").intValue = 255;
            };

            return list;
        }
    }
}
