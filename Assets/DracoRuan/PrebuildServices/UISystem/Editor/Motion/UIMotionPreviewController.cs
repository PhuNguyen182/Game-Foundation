using System;
using DracoRuan.PrebuildServices.UISystem.Motion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UIMotion = DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion.UIMotion;

namespace DracoRuan.PrebuildServices.UISystem.Editor.MotionTools
{
    public enum UIMotionPreviewState
    {
        Stopped = 0,
        Playing = 1,
        Paused = 2,
    }

    /// <summary>
    /// Owns the Edit-mode preview of one UIMotion at a time: the clock (EditorApplication.update),
    /// Play/Pause/Stop/scrub, AnimationMode, and the safety net that makes sure a preview never
    /// outlives what it is safe for - it stops (restoring every target) on selection change,
    /// entering Play mode, an assembly reload, and scene/prefab saving, so preview values can
    /// never end up saved into a scene or prefab. The actual sampling lives on the UIMotion itself
    /// (UIMotion.Preview.cs); this class only decides which time to ask for.
    /// </summary>
    public static class UIMotionPreviewController
    {
        public static event Action Changed;

        public static UIMotion Target { get; private set; }
        public static bool ShowTimeline { get; private set; }
        public static UIMotionPreviewState State { get; private set; }

        /// <summary>Current position on the timeline, in timeline seconds.</summary>
        public static float Time { get; private set; }

        /// <summary>Why the last Play was refused (e.g. AnimationMode is busy), else null.</summary>
        public static string Message { get; private set; }

        public static float Duration => Target ? Target.PreviewDuration : 0f;

        private static bool _ownsAnimationMode;
        private static bool _subscribed;
        private static double _lastClock;

        /// <summary>Starts previewing `show` (or Hide) from 0. Restarts if it is already running.</summary>
        public static void Play(UIMotion motion, bool show)
        {
            if (!motion || Application.isPlaying || !Begin(motion, show))
                return;

            Time = 0f;
            State = UIMotionPreviewState.Playing;
            _lastClock = EditorApplication.timeSinceStartup;
            Sample();
        }

        public static void Pause()
        {
            if (State != UIMotionPreviewState.Playing)
                return;

            State = UIMotionPreviewState.Paused;
            RaiseChanged();
        }

        public static void Resume()
        {
            if (State != UIMotionPreviewState.Paused || !Target)
                return;

            if (Time >= Duration)
                Time = 0f;

            State = UIMotionPreviewState.Playing;
            _lastClock = EditorApplication.timeSinceStartup;
            RaiseChanged();
        }

        /// <summary>Jumps to `time` and holds there (pauses if it was playing).</summary>
        public static void Scrub(UIMotion motion, float time)
        {
            if (Target != motion || State == UIMotionPreviewState.Stopped)
                return;

            State = UIMotionPreviewState.Paused;
            Time = Mathf.Clamp(time, 0f, Duration);
            Sample();
        }

        public static void Stop()
        {
            if (Target)
                Target.PreviewEnd();

            Target = null;
            State = UIMotionPreviewState.Stopped;
            Time = 0f;
            Message = null;

            UIMotionAnimationModeRecorder.Uninstall();
            if (_ownsAnimationMode)
            {
                AnimationMode.StopAnimationMode();
                _ownsAnimationMode = false;
            }

            Unsubscribe();
            RepaintViews();
            RaiseChanged();
        }

        /// <summary>Call after the Inspector edited `motion`'s tracks: rebuilds the running preview
        /// in place at the same time, so a tweak shows immediately without pressing Play again.</summary>
        public static void NotifyEdited(UIMotion motion)
        {
            if (Target != motion || State == UIMotionPreviewState.Stopped)
                return;

            motion.PreviewBegin(ShowTimeline);
            Time = Mathf.Clamp(Time, 0f, motion.PreviewDuration);
            Sample();
        }

        private static bool Begin(UIMotion motion, bool show)
        {
            if (Target && Target != motion)
                Stop();

            Message = null;

            if (!_ownsAnimationMode)
            {
                if (AnimationMode.InAnimationMode())
                {
                    Message = "Another window (for example Animation) is already using AnimationMode. Stop it first.";
                    RaiseChanged();
                    return false;
                }

                AnimationMode.StartAnimationMode();
                _ownsAnimationMode = true;
            }

            Subscribe();
            UIMotionAnimationModeRecorder.Install();
            Target = motion;
            ShowTimeline = show;
            motion.PreviewBegin(show);
            return true;
        }

        private static void Tick()
        {
            if (!Target)
            {
                Stop();
                return;
            }

            if (State != UIMotionPreviewState.Playing)
                return;

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastClock);
            _lastClock = now;

            Time = Mathf.Min(Time + delta * Target.PreviewSpeed, Target.PreviewDuration);
            Sample();

            // Hold on the last pose instead of snapping back, like a paused clip.
            if (Time >= Target.PreviewDuration || Target.PreviewFailed)
                State = UIMotionPreviewState.Paused;
        }

        private static void Sample()
        {
            if (!Target)
                return;

            Target.PreviewSeek(Time);
            Canvas.ForceUpdateCanvases();
            RepaintViews();
            RaiseChanged();
        }

        private static void RepaintViews()
        {
            SceneView.RepaintAll();
            InternalEditorUtility.RepaintAllViews();
        }

        private static void RaiseChanged() => Changed?.Invoke();

        private static void Subscribe()
        {
            if (_subscribed)
                return;

            _subscribed = true;
            EditorApplication.update += Tick;
            Selection.selectionChanged += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }

        private static void Unsubscribe()
        {
            if (!_subscribed)
                return;

            _subscribed = false;
            EditorApplication.update -= Tick;
            Selection.selectionChanged -= Stop;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            PrefabStage.prefabSaving -= OnPrefabSaving;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                Stop();
        }

        private static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) => Stop();

        private static void OnPrefabSaving(GameObject prefabRoot) => Stop();
    }
}
