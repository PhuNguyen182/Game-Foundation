using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Drawing
{
    /// <summary>Which platform's half of a Custom Pattern / Curve pair is currently shown.</summary>
    public enum VibrationPlatform
    {
        IOS,
        Android
    }

    /// <summary>
    /// A two-button toolbar for switching between a mode's iOS and Android data, with a warning badge
    /// on whichever side has nothing authored.
    /// </summary>
    /// <remarks>
    /// This is the fix for the bug that started the rebuild: Odin's default composite drawer rendered
    /// iOS and Android as two nested struct foldouts inside a <c>[ShowIf]</c> fade group, and expanding
    /// both at once produced runaway heights and vanishing fields. Showing exactly one platform's flat
    /// fields at a time removes the nesting instead of animating around it.
    /// </remarks>
    public static class VibrationPlatformTabs
    {
        public static VibrationPlatform Draw(VibrationPlatform current, bool iosHasWarning, bool androidHasWarning)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawTab(current == VibrationPlatform.IOS, "iOS", iosHasWarning))
                    current = VibrationPlatform.IOS;

                if (DrawTab(current == VibrationPlatform.Android, "Android", androidHasWarning))
                    current = VibrationPlatform.Android;
            }

            return current;
        }

        private static bool DrawTab(bool isSelected, string label, bool hasWarning)
        {
            string text = hasWarning ? $"{label} ⚠" : label;
            return GUILayout.Toggle(isSelected, text, EditorStyles.toolbarButton);
        }
    }
}
