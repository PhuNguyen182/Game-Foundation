using DracoRuan.PrebuildServices.MobileVibration.Data;
using UnityEditor;
using UnityEngine;
#if USE_MOST_HAPTICS
using Solo.MOST_IN_ONE;
#endif

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Drawing
{
    /// <summary>
    /// Draws the Play/Stop row for a <see cref="VibrationEntry"/> and dispatches to
    /// <c>MOST_HapticFeedback</c> exactly as <c>VibrationService</c> would at runtime.
    /// </summary>
    /// <remarks>
    /// <para>Every native call the plugin makes is compiled out under <c>!UNITY_EDITOR</c>
    /// (see <c>MOST_HapticFeedback.cs</c>'s platform guards), so Play is a no-op here. It exists so the
    /// button layout matches a device build and the warning is visible at the point someone reaches
    /// for it, not just in a doc comment.</para>
    ///
    /// <para>Without <c>USE_MOST_HAPTICS</c> the buttons still draw, but Play and Stop are no-ops —
    /// same shape as <c>VibrationService</c> when the plugin is not installed.</para>
    /// </remarks>
    public static class VibrationPreviewPlayer
    {
        public static void Draw(VibrationEntry entry)
        {
#if USE_MOST_HAPTICS
            EditorGUILayout.HelpBox("Play calls MOST_HapticFeedback directly, but every native call the "
                                    + "plugin makes is compiled out under !UNITY_EDITOR. Nothing vibrates "
                                    + "here — build to a device (or use Unity Remote) to feel it.",
                MessageType.Info);
#else
            EditorGUILayout.HelpBox("The MOST Haptics plugin is not installed (USE_MOST_HAPTICS is off), "
                                    + "so Play/Stop here are no-ops.", MessageType.Warning);
#endif

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!entry.IsPlayable(out _)))
                {
                    if (GUILayout.Button("▶ Play"))
                        Play(entry);
                }

                if (GUILayout.Button("■ Stop"))
                {
#if USE_MOST_HAPTICS
                    MOST_HapticFeedback.Stop();
#endif
                }
            }
        }

        private static void Play(VibrationEntry entry)
        {
#if USE_MOST_HAPTICS
            switch (entry.SourceMode)
            {
                case VibrationSourceMode.Preset:
                    MOST_HapticFeedback.Generate(entry.PresetType);
                    break;
                case VibrationSourceMode.CustomPattern:
                    MOST_HapticFeedback.Generate(entry.CustomPattern);
                    break;
                case VibrationSourceMode.Curve:
                    MOST_HapticFeedback.Generate(entry.Curve);
                    break;
            }
#endif
        }
    }
}