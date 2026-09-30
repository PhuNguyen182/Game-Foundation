using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor.DracoRuan.PrebuildServices.UISystem.Editor
{
    /// <summary>REWRITE_PLAN.md mục 5 bước 8, "UI Debugger": stack, queue, input lock, active
    /// VMs at a glance in Play mode. Reads UIService.Current (see its doc comment) - there is
    /// nothing to show outside Play mode, since the service doesn't exist yet.</summary>
    public sealed class UIDebuggerWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Tools/DracoRuan/UISystem/UI Debugger")]
        private static void Open() => GetWindow<UIDebuggerWindow>("UI Debugger");

        private void OnEnable() => EditorApplication.update += this.Repaint;

        private void OnDisable() => EditorApplication.update -= this.Repaint;

        private void OnGUI()
        {
            UIService service = UIService.Current;
            if (service == null)
            {
                EditorGUILayout.HelpBox("No UIService is currently running - enter Play mode.", MessageType.Info);
                return;
            }

            UIServiceDebugSnapshot snapshot = service.CaptureDebugSnapshot();

            this._scroll = EditorGUILayout.BeginScrollView(this._scroll);

            EditorGUILayout.LabelField("Input Lock Count", snapshot.InputLockCount.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Screen Stack (bottom to top)", EditorStyles.boldLabel);
            foreach (string name in snapshot.ScreenStack)
                EditorGUILayout.LabelField("  " + name);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Popups by Layer", EditorStyles.boldLabel);
            foreach (KeyValuePair<string, IReadOnlyList<string>> pair in snapshot.PopupsByLayer)
            {
                EditorGUILayout.LabelField(pair.Key);
                foreach (string name in pair.Value)
                    EditorGUILayout.LabelField("  " + name);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Queues by Layer", EditorStyles.boldLabel);
            foreach (KeyValuePair<string, int> pair in snapshot.QueueCountByLayer)
            {
                bool paused = snapshot.QueuePausedByLayer.TryGetValue(pair.Key, out bool p) && p;
                EditorGUILayout.LabelField($"{pair.Key}: {pair.Value} queued{(paused ? " (paused)" : "")}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Open View Models", EditorStyles.boldLabel);
            foreach (string name in snapshot.OpenViewModelTypeNames)
                EditorGUILayout.LabelField("  " + name);

            EditorGUILayout.EndScrollView();
        }
    }
}
