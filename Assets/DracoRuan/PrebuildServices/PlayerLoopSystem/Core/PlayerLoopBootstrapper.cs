using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;
using UnityEngine;
using UnityEngine.LowLevel;
using FixedUpdate = UnityEngine.PlayerLoop.FixedUpdate;
using LoopSystem = UnityEngine.LowLevel.PlayerLoopSystem;
using Update = UnityEngine.PlayerLoop.Update;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.Core
{
    /// <summary>Loop entry of <see cref="UpdateServiceManager"/>; its own type so it never collides with Unity's <c>Update</c>.</summary>
    internal struct DracoUpdateSystem
    {
    }

    /// <summary>Loop entry of <see cref="FixedUpdateServiceManager"/>.</summary>
    internal struct DracoFixedUpdateSystem
    {
    }

    /// <summary>
    /// Hooks the service managers into the player loop: one system at the start of <c>Update</c> and one at the start of
    /// <c>FixedUpdate</c>, both ahead of the scripts' own <c>Update</c> / <c>FixedUpdate</c>.
    /// </summary>
    public static class PlayerLoopBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Initialize()
        {
            LoopSystem currentLoopSystem = PlayerLoop.GetCurrentPlayerLoop();

            // Safe to call repeatedly: with Enter Play Mode Options (no domain reload) the previous session's systems are
            // still in the loop and would otherwise tick the managers once more per frame for every play session.
            RemoveOwnSystems(ref currentLoopSystem);

            LoopSystem updateSystem = CreateUpdateSystem();
            if (!PlayerLoopUtils.InsertSystemBefore<Update, Update.ScriptRunBehaviourUpdate>(
                    ref currentLoopSystem, in updateSystem))
            {
                Debug.LogError("Failed to insert Update system to PlayerLoop.");
                return;
            }

            LoopSystem fixedUpdateSystem = CreateFixedUpdateSystem();
            if (!PlayerLoopUtils.InsertSystemBefore<FixedUpdate, FixedUpdate.ScriptRunBehaviourFixedUpdate>(
                    ref currentLoopSystem, in fixedUpdateSystem))
            {
                Debug.LogError("Failed to insert FixedUpdate system to PlayerLoop.");
                return;
            }

            PlayerLoop.SetPlayerLoop(currentLoopSystem);

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        /// <summary>Takes both systems out of the player loop and drops every registered handler.</summary>
        internal static void Shutdown()
        {
            LoopSystem currentLoopSystem = PlayerLoop.GetCurrentPlayerLoop();
            RemoveOwnSystems(ref currentLoopSystem);
            PlayerLoop.SetPlayerLoop(currentLoopSystem);

            UpdateServiceManager.Clear();
            FixedUpdateServiceManager.Clear();
        }

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                Shutdown();
        }
#endif

        private static void RemoveOwnSystems(ref LoopSystem playerLoopSystem)
        {
            LoopSystem updateSystem = CreateUpdateSystem();
            LoopSystem fixedUpdateSystem = CreateFixedUpdateSystem();

            PlayerLoopUtils.RemoveSystem<Update>(ref playerLoopSystem, in updateSystem);
            PlayerLoopUtils.RemoveSystem<FixedUpdate>(ref playerLoopSystem, in fixedUpdateSystem);
        }

        private static LoopSystem CreateUpdateSystem() => new()
        {
            type = typeof(DracoUpdateSystem),
            updateDelegate = TickUpdate,
            subSystemList = null
        };

        private static LoopSystem CreateFixedUpdateSystem() => new()
        {
            type = typeof(DracoFixedUpdateSystem),
            updateDelegate = TickFixedUpdate,
            subSystemList = null
        };

        private static void TickUpdate() => UpdateServiceManager.UpdateTime();

        private static void TickFixedUpdate() => FixedUpdateServiceManager.FixedUpdateTime();
    }
}
