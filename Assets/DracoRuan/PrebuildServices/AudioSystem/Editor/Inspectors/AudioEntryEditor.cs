using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor.Inspectors
{
    /// <summary>
    /// Adds an identifier health banner above the normal <see cref="AudioEntry"/> inspector.
    /// </summary>
    /// <remarks>
    /// The same <see cref="AudioIdCollisionDetector"/> the window and the generator use, so an
    /// entry cannot look fine here and be refused there. Editing the id from the Inspector is
    /// allowed; the banner is what makes the consequence visible without opening the tool.
    /// </remarks>
    [CustomEditor(typeof(AudioEntry))]
    [CanEditMultipleObjects]
    public sealed class AudioEntryEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            if (this.targets.Length == 1)
                this.DrawIdBanner((AudioEntry)this.target);

            base.OnInspectorGUI();
        }

        private void DrawIdBanner(AudioEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Id))
            {
                EditorGUILayout.HelpBox("This entry has no identifier, so no constant is generated for it "
                                        + "and it cannot be played by name.", MessageType.Warning);
                return;
            }

            AudioIdSanitizeResult sanitized = AudioIdSanitizer.ToMemberName(entry.Id);

            if (sanitized.Status == AudioIdStatus.Rejected)
            {
                EditorGUILayout.HelpBox(sanitized.Message, MessageType.Error);
                return;
            }

            string ownPath = AssetDatabase.GetAssetPath(entry);
            AudioIdConflict? conflict = AudioIdCollisionDetector.CheckAgainst(
                entry.Id, AudioIdIndex.Entries, ignoreOwnerPath: ownPath);

            if (conflict.HasValue)
            {
                EditorGUILayout.HelpBox(conflict.Value.Message, MessageType.Error);
                return;
            }

            if (sanitized.Status == AudioIdStatus.Adjusted)
            {
                EditorGUILayout.HelpBox(sanitized.Message, MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox($"Generates as AudioId.{sanitized.MemberName}", MessageType.None);
        }
    }
}
