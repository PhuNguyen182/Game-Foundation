using UnityEditor;
using UnityEngine;
#if USE_MOST_HAPTICS
using Solo.MOST_IN_ONE;
#endif

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Drawing
{
    /// <summary>
    /// Draws one platform's half of <c>MOST_HapticFeedback.HapticCurve</c> (<c>iOSHapticCurve</c> or
    /// <c>AndroidHapticCurve</c>) as flat fields, by name, through <see cref="SerializedProperty"/>.
    /// </summary>
    /// <remarks>
    /// <para>Every field is drawn with a concrete control instead of a generic
    /// <c>EditorGUILayout.PropertyField(property, includeChildren: true)</c> on the struct, so nothing
    /// here goes through Odin or Unity's own composite-struct foldout — the thing that broke in the
    /// window this replaces. Ranges are enforced explicitly because <c>[Min]</c> does not constrain a
    /// <c>long</c> field the way it does a <c>float</c> or <c>int</c> one (see
    /// <c>AndroidHapticCurve.Delay</c>/<c>DurationMs</c>).</para>
    ///
    /// <para>The min-duration and max-samples clamps read from <c>MOST_HapticFeedback</c>'s own
    /// constants, so under <c>USE_MOST_HAPTICS</c> they only exist there; without the plugin the
    /// fields are still drawn, just unclamped against those specific limits.</para>
    /// </remarks>
    public static class VibrationCurveSection
    {
        public static void DrawIOS(SerializedProperty curveProperty)
        {
            SerializedProperty delay = curveProperty.FindPropertyRelative("Delay");
            SerializedProperty durationMs = curveProperty.FindPropertyRelative("DurationMs");
            SerializedProperty sharpness = curveProperty.FindPropertyRelative("Sharpness");
            SerializedProperty samples = curveProperty.FindPropertyRelative("Samples");
            SerializedProperty intensity = curveProperty.FindPropertyRelative("Intensity");
            SerializedProperty fallbackType = curveProperty.FindPropertyRelative("FallbackType");

            delay.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("Delay (ms)", delay.floatValue));
#if USE_MOST_HAPTICS
            durationMs.floatValue = Mathf.Max(MOST_HapticFeedback.MinCurveDurationMs,
                EditorGUILayout.FloatField("Duration (ms)", durationMs.floatValue));
#else
            durationMs.floatValue = EditorGUILayout.FloatField("Duration (ms)", durationMs.floatValue);
#endif
            sharpness.floatValue = EditorGUILayout.Slider("Sharpness", sharpness.floatValue, 0f, 1f);
#if USE_MOST_HAPTICS
            samples.intValue = EditorGUILayout.IntSlider(
                "Samples", samples.intValue, 2, MOST_HapticFeedback.MaxCurveSamples);
#else
            samples.intValue = Mathf.Max(2, EditorGUILayout.IntField("Samples", samples.intValue));
#endif
            EditorGUILayout.PropertyField(intensity, new GUIContent("Intensity"));
            EditorGUILayout.PropertyField(fallbackType, new GUIContent("Fallback"));
        }

        public static void DrawAndroid(SerializedProperty curveProperty)
        {
            SerializedProperty delay = curveProperty.FindPropertyRelative("Delay");
            SerializedProperty durationMs = curveProperty.FindPropertyRelative("DurationMs");
            SerializedProperty samples = curveProperty.FindPropertyRelative("Samples");
            SerializedProperty maxAmplitude = curveProperty.FindPropertyRelative("MaxAmplitude");
            SerializedProperty intensity = curveProperty.FindPropertyRelative("Intensity");
            SerializedProperty fallbackType = curveProperty.FindPropertyRelative("FallbackType");

            delay.longValue = System.Math.Max(0L, EditorGUILayout.LongField("Delay (ms)", delay.longValue));
#if USE_MOST_HAPTICS
            durationMs.longValue = System.Math.Max((long)MOST_HapticFeedback.MinCurveDurationMs,
                EditorGUILayout.LongField("Duration (ms)", durationMs.longValue));
#else
            durationMs.longValue =
                System.Math.Max(0L, EditorGUILayout.LongField("Duration (ms)", durationMs.longValue));
#endif
#if USE_MOST_HAPTICS
            samples.intValue = EditorGUILayout.IntSlider(
                "Samples", samples.intValue, 2, MOST_HapticFeedback.MaxCurveSamples);
#else
            samples.intValue = Mathf.Max(2, EditorGUILayout.IntField("Samples", samples.intValue));
#endif
            maxAmplitude.intValue = EditorGUILayout.IntSlider("Max Amplitude", maxAmplitude.intValue, 1, 255);
            EditorGUILayout.PropertyField(intensity, new GUIContent("Intensity"));
            EditorGUILayout.PropertyField(fallbackType, new GUIContent("Fallback"));
        }
    }
}