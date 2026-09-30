using System;
using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// Stack of scene cameras the UI camera should render on top of (CAMERA_INPUT_PLAN.md,
    /// Quyết định 2). Static so a UIBaseCameraBinder can register before UIService exists.
    /// The last registered camera wins; unregistering it hands the UI back to the previous one,
    /// so a cutscene or additive scene can temporarily override and restore.
    /// </summary>
    public static class UIBaseCameras
    {
        private static readonly List<Camera> Stack = new();

        public static event Action Changed;

        /// <summary>Topmost live registered camera, or null.</summary>
        public static Camera Current
        {
            get
            {
                Stack.RemoveAll(camera => !camera);
                return Stack.Count > 0 ? Stack[^1] : null;
            }
        }

        public static void Register(Camera camera)
        {
            if (!camera)
                return;

            Stack.Remove(camera);
            Stack.Add(camera);
            Changed?.Invoke();
        }

        public static void Unregister(Camera camera)
        {
            if (Stack.Remove(camera))
                Changed?.Invoke();
        }

        internal static void Clear()
        {
            Stack.Clear();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            Stack.Clear();
            Changed = null;
        }
    }
}
