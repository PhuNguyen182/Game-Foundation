using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using Sirenix.OdinInspector.Editor;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Inspectors
{
    /// <summary>
    /// Adds an identifier health banner and a Play/Stop preview above the normal
    /// <see cref="VibrationEntry"/> inspector.
    /// </summary>
    /// <remarks>
    /// <para>The same <see cref="VibrationIdCollisionDetector"/> the window and the generator use, so
    /// an entry cannot look fine here and be refused there — the same role
    /// <c>AudioEntryEditor</c>'s banner plays for audio.</para>
    ///
    /// <para><b>The preview buttons are no-ops in the desktop Editor.</b> They call
    /// <c>MOST_HapticFeedback</c> directly, but every native call in that plugin is compiled out
    /// under <c>!UNITY_EDITOR</c> (see <c>MOST_HapticFeedback.cs</c>'s <c>#if UNITY_IOS &amp;&amp;
    /// !UNITY_EDITOR</c> / <c>#if UNITY_ANDROID &amp;&amp; !UNITY_EDITOR</c> guards). Pressing Play
    /// here produces no physical vibration; build to a device, or use Unity Remote, to feel it.</para>
    /// </remarks>
    [CustomEditor(typeof(VibrationEntry))]
    [CanEditMultipleObjects]
    public sealed class VibrationEntryEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            if (this.targets.Length != 1)
            {
                base.OnInspectorGUI();
                return;
            }

            VibrationEntry entry = (VibrationEntry)this.target;

            this.DrawIdBanner(entry);
            base.OnInspectorGUI();
            this.DrawPreview(entry);
        }

        private void DrawIdBanner(VibrationEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Id))
            {
                EditorGUILayout.HelpBox("This entry has no identifier, so no constant is generated for it "
                                        + "and it cannot be played by name.", MessageType.Warning);
                return;
            }

            VibrationIdSanitizeResult sanitized = VibrationIdSanitizer.ToMemberName(entry.Id);

            if (sanitized.Status == VibrationIdStatus.Rejected)
            {
                EditorGUILayout.HelpBox(sanitized.Message, MessageType.Error);
                return;
            }

            string ownPath = AssetDatabase.GetAssetPath(entry);
            VibrationIdConflict? conflict = VibrationIdCollisionDetector.CheckAgainst(
                entry.Id, VibrationIdIndex.Entries, ignoreOwnerPath: ownPath);

            if (conflict.HasValue)
            {
                EditorGUILayout.HelpBox(conflict.Value.Message, MessageType.Error);
                return;
            }

            if (sanitized.Status == VibrationIdStatus.Adjusted)
            {
                EditorGUILayout.HelpBox(sanitized.Message, MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox($"Generates as VibrationId.{sanitized.MemberName}", MessageType.None);
        }

        private void DrawPreview(VibrationEntry entry)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox("Play and Stop call MOST_HapticFeedback directly, but every native "
                                    + "call the plugin makes is compiled out under !UNITY_EDITOR. Nothing "
                                    + "vibrates here — build to a device (or use Unity Remote) to feel it.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!entry.IsPlayable(out _)))
                {
                    if (GUILayout.Button("▶ Play"))
                        PlayPreview(entry);
                }

                if (GUILayout.Button("■ Stop"))
                    MOST_HapticFeedback.Stop();
            }
        }

        private static void PlayPreview(VibrationEntry entry)
        {
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
        }
    }
}
