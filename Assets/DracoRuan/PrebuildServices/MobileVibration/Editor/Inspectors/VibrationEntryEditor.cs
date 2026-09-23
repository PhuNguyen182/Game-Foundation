using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Editor.Drawing;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Inspectors
{
    /// <summary>
    /// The Inspector for a <see cref="VibrationEntry"/>: an identifier health banner, then the same
    /// <see cref="VibrationEntryDrawer"/> the Vibration Manager window uses.
    /// </summary>
    /// <remarks>
    /// Plain <see cref="UnityEditor.Editor"/>, not <c>OdinEditor</c>: nothing here goes through Odin
    /// any more, so there is nothing left for that base class to add.
    /// </remarks>
    [CustomEditor(typeof(VibrationEntry))]
    [CanEditMultipleObjects]
    public sealed class VibrationEntryEditor : UnityEditor.Editor
    {
        private VibrationEntryDrawer _drawer;

        public override void OnInspectorGUI()
        {
            if (this.targets.Length != 1)
            {
                this.DrawDefaultInspector();
                return;
            }

            VibrationEntry entry = (VibrationEntry)this.target;

            this.DrawIdBanner(entry);
            EditorGUILayout.Space(6f);

            this._drawer ??= new VibrationEntryDrawer();
            this._drawer.Draw(this.serializedObject);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Open in Vibration Manager"))
            {
                VibrationManagerWindow.ShowWindow();
                VibrationManagerWindow.Select(entry);
            }
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
    }
}