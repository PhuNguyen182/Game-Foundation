using DracoRuan.PrebuildServices.UISystem.Motion;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor.MotionTools
{
    /// <summary>
    /// Inspector for UIMotion: an Edit-mode preview toolbar (Play Show / Play Hide / Pause / Stop /
    /// scrub), the trigger and speed settings, then the Show and Hide track lists where each track
    /// is a collapsible block (UIMotionTrackDrawer) so a long list stays scannable.
    /// </summary>
    [CustomEditor(typeof(UIMotion))]
    public sealed class UIMotionEditor : UnityEditor.Editor
    {
        private SerializedProperty _trigger;
        private SerializedProperty _speedOverride;
        private SerializedProperty _mirrorHide;
        private SerializedProperty _showTracks;
        private SerializedProperty _hideTracks;
        private ReorderableList _showList;
        private ReorderableList _hideList;

        private void OnEnable()
        {
            this._trigger = this.serializedObject.FindProperty("trigger");
            this._speedOverride = this.serializedObject.FindProperty("speedOverride");
            this._mirrorHide = this.serializedObject.FindProperty("mirrorHide");
            this._showTracks = this.serializedObject.FindProperty("showTracks");
            this._hideTracks = this.serializedObject.FindProperty("hideTracks");
            this._showList = this.BuildList(this._showTracks);
            this._hideList = this.BuildList(this._hideTracks);

            UIMotionPreviewController.Changed += this.Repaint;
        }

        private void OnDisable() => UIMotionPreviewController.Changed -= this.Repaint;

        public override void OnInspectorGUI()
        {
            var motion = (UIMotion)this.target;
            this.serializedObject.Update();

            this.DrawPreviewToolbar(motion);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(this._trigger);
            EditorGUILayout.PropertyField(this._speedOverride);
            EditorGUILayout.PropertyField(this._mirrorHide);
            EditorGUILayout.Space();

            this.DrawSection("Show Tracks", this._showTracks, this._showList);

            if (this._mirrorHide.boolValue)
                EditorGUILayout.HelpBox("Hide plays a reversed copy of Show, so it has no tracks of its own.", MessageType.Info);
            else
                this.DrawSection("Hide Tracks", this._hideTracks, this._hideList);

            bool changed = EditorGUI.EndChangeCheck();
            this.serializedObject.ApplyModifiedProperties();

            if (changed)
                UIMotionPreviewController.NotifyEdited(motion);
        }

        private ReorderableList BuildList(SerializedProperty tracks)
        {
            var list = new ReorderableList(this.serializedObject, tracks, true, false, true, true)
            {
                elementHeightCallback = index =>
                    EditorGUI.GetPropertyHeight(tracks.GetArrayElementAtIndex(index), GUIContent.none, true) + 6f,

                drawElementCallback = (rect, index, _, _) =>
                {
                    // Leave room for the foldout arrow, which sits at the drawer's left edge.
                    rect.y += 3f;
                    rect.x += 10f;
                    rect.width -= 10f;
                    EditorGUI.PropertyField(rect, tracks.GetArrayElementAtIndex(index), GUIContent.none, true);
                },

                onAddCallback = l =>
                {
                    SerializedProperty array = l.serializedProperty;
                    int index = array.arraySize;
                    array.arraySize++;

                    // Unity clones the previous element on add; a new track should start from
                    // the class defaults instead.
                    SerializedProperty added = array.GetArrayElementAtIndex(index);
                    added.boxedValue = new UIMotionTrack();
                    added.isExpanded = true;
                    l.index = index;
                },
            };

            return list;
        }

        private void DrawSection(string title, SerializedProperty tracks, ReorderableList list)
        {
            EditorGUILayout.BeginHorizontal();
            tracks.isExpanded = EditorGUILayout.Foldout(tracks.isExpanded, title + " (" + tracks.arraySize + ")", true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Expand all", EditorStyles.miniButtonLeft, GUILayout.Width(74f)))
                SetAllExpanded(tracks, true);
            if (GUILayout.Button("Collapse all", EditorStyles.miniButtonRight, GUILayout.Width(80f)))
                SetAllExpanded(tracks, false);
            EditorGUILayout.EndHorizontal();

            if (tracks.isExpanded)
                list.DoLayoutList();
        }

        private static void SetAllExpanded(SerializedProperty tracks, bool expanded)
        {
            for (int i = 0; i < tracks.arraySize; i++)
                tracks.GetArrayElementAtIndex(i).isExpanded = expanded;
        }

        private void DrawPreviewToolbar(UIMotion motion)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Preview runs in Edit mode only.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            bool mine = UIMotionPreviewController.Target == motion;
            UIMotionPreviewState state = mine ? UIMotionPreviewController.State : UIMotionPreviewState.Stopped;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play Show"))
                UIMotionPreviewController.Play(motion, true);
            if (GUILayout.Button("Play Hide"))
                UIMotionPreviewController.Play(motion, false);

            using (new EditorGUI.DisabledScope(state == UIMotionPreviewState.Stopped))
            {
                if (GUILayout.Button(state == UIMotionPreviewState.Paused ? "Resume" : "Pause"))
                {
                    if (state == UIMotionPreviewState.Paused)
                        UIMotionPreviewController.Resume();
                    else
                        UIMotionPreviewController.Pause();
                }

                if (GUILayout.Button("Stop"))
                    UIMotionPreviewController.Stop();
            }

            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(state == UIMotionPreviewState.Stopped))
            {
                float duration = mine ? UIMotionPreviewController.Duration : 0f;
                EditorGUI.BeginChangeCheck();
                float time = EditorGUILayout.Slider("Time", mine ? UIMotionPreviewController.Time : 0f, 0f, Mathf.Max(duration, 0.0001f));
                if (EditorGUI.EndChangeCheck())
                    UIMotionPreviewController.Scrub(motion, time);
            }

            if (!string.IsNullOrEmpty(UIMotionPreviewController.Message) && !mine)
                EditorGUILayout.HelpBox(UIMotionPreviewController.Message, MessageType.Warning);

            if (mine && motion.PreviewSkippedTrackCount > 0)
                EditorGUILayout.HelpBox(
                    motion.PreviewSkippedTrackCount + " AnimatorState track(s) are not previewed in Edit mode; they still run in Play mode.",
                    MessageType.Info);

            if (mine && motion.PreviewFailed)
                EditorGUILayout.HelpBox("A track threw while previewing (see the Console). Fix it, then press Play again.", MessageType.Warning);

            EditorGUILayout.EndVertical();
        }
    }
}
