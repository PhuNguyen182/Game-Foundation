using System.Collections.Generic;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Drawing
{
    /// <summary>
    /// Draws one <see cref="VibrationEntry"/>'s editable fields: Source Mode, the mode's own section,
    /// Throttling, a timeline preview and Play/Stop.
    /// </summary>
    /// <remarks>
    /// <para>Shared by <c>VibrationManagerWindow</c>'s detail pane and <c>VibrationEntryEditor</c>'s
    /// Inspector, so the two surfaces cannot drift into showing the entry differently.</para>
    ///
    /// <para>Everything below Source Mode is drawn through explicit <see cref="SerializedProperty"/>
    /// field-by-field calls, never a generic composite drawer on a struct — see
    /// <see cref="VibrationCurveSection"/> and <see cref="VibrationPatternSection"/> for why. One
    /// instance is kept per entry being shown (both callers hold it across repaints) because
    /// <see cref="VibrationPatternSection"/> caches stateful <c>ReorderableList</c>s.</para>
    /// </remarks>
    public sealed class VibrationEntryDrawer
    {
        private readonly VibrationPatternSection _patternSection = new VibrationPatternSection();

        private VibrationPlatform _platform =
            (VibrationPlatform)SessionState.GetInt(VibrationEditorState.SelectedPlatformKey, 0);

        public void Draw(SerializedObject serializedObject)
        {
            serializedObject.Update();

            SerializedProperty sourceMode = serializedObject.FindProperty("sourceMode");
            this.DrawModeToolbar(sourceMode);

            EditorGUILayout.Space(4f);

            switch ((VibrationSourceMode)sourceMode.enumValueIndex)
            {
                case VibrationSourceMode.Preset:
                    this.DrawPreset(serializedObject);
                    break;
                case VibrationSourceMode.CustomPattern:
                    this.DrawCustomPattern(serializedObject);
                    break;
                case VibrationSourceMode.Curve:
                    this.DrawCurve(serializedObject);
                    break;
            }

            EditorGUILayout.Space(8f);
            this.DrawThrottling(serializedObject);

            serializedObject.ApplyModifiedProperties();

            if (serializedObject.targetObject is VibrationEntry entry)
            {
                EditorGUILayout.Space(8f);
                VibrationPreviewPlayer.Draw(entry);
            }
        }

        private void DrawModeToolbar(SerializedProperty sourceMode)
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            string[] labels = { "Preset", "Custom Pattern", "Curve" };
            int selected = GUILayout.Toolbar(sourceMode.enumValueIndex, labels, EditorStyles.toolbarButton);

            if (selected != sourceMode.enumValueIndex)
                sourceMode.enumValueIndex = selected;
        }

        private void DrawPreset(SerializedObject serializedObject)
        {
            SerializedProperty presetType = serializedObject.FindProperty("presetType");
            EditorGUILayout.PropertyField(presetType, new GUIContent("Preset Type"));

            EditorGUILayout.Space(6f);
            VibrationTimelineView.Draw(VibrationTimeline.FromPulses(
                new[] { 0f }, new[] { 0f }, new[] { 1f }));
        }

        private void DrawCustomPattern(SerializedObject serializedObject)
        {
            SerializedProperty pattern = serializedObject.FindProperty("customPattern");
            SerializedProperty iosPulses = pattern.FindPropertyRelative("IOS_HapticPattern");
            SerializedProperty androidPulses = pattern.FindPropertyRelative("Android_HapticPattern");

            this._platform = VibrationPlatformTabs.Draw(
                this._platform, iosPulses.arraySize == 0, androidPulses.arraySize == 0);
            SessionState.SetInt(VibrationEditorState.SelectedPlatformKey, (int)this._platform);

            EditorGUILayout.Space(4f);

            if (this._platform == VibrationPlatform.IOS)
                this._patternSection.DrawIOS(serializedObject, iosPulses);
            else
                this._patternSection.DrawAndroid(serializedObject, androidPulses);

            EditorGUILayout.Space(6f);
            VibrationTimelineView.Draw(this._platform == VibrationPlatform.IOS
                ? BuildIOSPatternTimeline(iosPulses)
                : BuildAndroidPatternTimeline(androidPulses));
        }

        private void DrawCurve(SerializedObject serializedObject)
        {
            SerializedProperty curve = serializedObject.FindProperty("curve");
            SerializedProperty iosCurve = curve.FindPropertyRelative("IOS_HapticCurve");
            SerializedProperty androidCurve = curve.FindPropertyRelative("Android_HapticCurve");

            bool iosEmpty = iosCurve.FindPropertyRelative("Intensity").animationCurveValue.length == 0;
            bool androidEmpty = androidCurve.FindPropertyRelative("Intensity").animationCurveValue.length == 0;

            this._platform = VibrationPlatformTabs.Draw(this._platform, iosEmpty, androidEmpty);
            SessionState.SetInt(VibrationEditorState.SelectedPlatformKey, (int)this._platform);

            EditorGUILayout.Space(4f);

            if (this._platform == VibrationPlatform.IOS)
                VibrationCurveSection.DrawIOS(iosCurve);
            else
                VibrationCurveSection.DrawAndroid(androidCurve);

            EditorGUILayout.Space(6f);
            VibrationTimelineView.Draw(this._platform == VibrationPlatform.IOS
                ? BuildCurveTimeline(iosCurve)
                : BuildCurveTimeline(androidCurve));
        }

        private void DrawThrottling(SerializedObject serializedObject)
        {
            EditorGUILayout.LabelField("Throttling", EditorStyles.boldLabel);

            SerializedProperty minInterval = serializedObject.FindProperty("minIntervalSeconds");
            minInterval.floatValue = Mathf.Max(0f,
                EditorGUILayout.FloatField("Min Interval (s)", minInterval.floatValue));
        }

        private static VibrationTimeline BuildIOSPatternTimeline(SerializedProperty pulses)
        {
            int count = pulses.arraySize;
            float[] delays = new float[count];

            for (int i = 0; i < count; i++)
                delays[i] = pulses.GetArrayElementAtIndex(i).FindPropertyRelative("Delay").floatValue;

            return VibrationTimeline.FromPulses(delays, new float[count], Ones(count));
        }

        private static VibrationTimeline BuildAndroidPatternTimeline(SerializedProperty pulses)
        {
            int count = pulses.arraySize;
            float[] delays = new float[count];
            float[] durations = new float[count];
            float[] strengths = new float[count];

            for (int i = 0; i < count; i++)
            {
                SerializedProperty pulse = pulses.GetArrayElementAtIndex(i);
                delays[i] = pulse.FindPropertyRelative("Delay").longValue;
                durations[i] = pulse.FindPropertyRelative("PulseTime").longValue;
                strengths[i] = pulse.FindPropertyRelative("PulseStrength").intValue / 255f;
            }

            return VibrationTimeline.FromPulses(delays, durations, strengths);
        }

        private static VibrationTimeline BuildCurveTimeline(SerializedProperty curveProperty)
        {
            SerializedProperty delayProperty = curveProperty.FindPropertyRelative("Delay");
            SerializedProperty durationProperty = curveProperty.FindPropertyRelative("DurationMs");
            AnimationCurve intensity = curveProperty.FindPropertyRelative("Intensity").animationCurveValue;

            float delayMs = delayProperty.propertyType == SerializedPropertyType.Float
                ? delayProperty.floatValue
                : delayProperty.longValue;
            float durationMs = durationProperty.propertyType == SerializedPropertyType.Float
                ? durationProperty.floatValue
                : durationProperty.longValue;

            if (intensity == null || intensity.length == 0 || durationMs <= 0f)
                return VibrationTimeline.Empty;

            const int sampleCount = 32;
            List<float> samples = new List<float>(sampleCount);

            for (int i = 0; i < sampleCount; i++)
                samples.Add(intensity.Evaluate(i / (float)(sampleCount - 1)));

            return VibrationTimeline.FromCurve(delayMs, durationMs, samples);
        }

        private static float[] Ones(int count)
        {
            float[] values = new float[count];
            for (int i = 0; i < count; i++)
                values[i] = 1f;
            return values;
        }
    }
}