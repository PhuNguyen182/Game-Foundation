using DracoRuan.CoreSystems.PlayerLoopSystem.UpdateServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using FixedUpdate = UnityEngine.PlayerLoop.FixedUpdate;
using Update = UnityEngine.PlayerLoop.Update;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.Core
{
    public static class PlayerLoopBootstrapper
    {
        private static UnityEngine.LowLevel.PlayerLoopSystem _playerLoopSystem;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Initialize()
        {
            UnityEngine.LowLevel.PlayerLoopSystem currentLoopSystem = PlayerLoop.GetCurrentPlayerLoop();

            if (!InsertUpdateSystem<Update>(ref currentLoopSystem, 0))
            {
                Debug.LogError("Failed to insert Update system to PlayerLoop.");
                return;
            }

            if (!InsertFixedUpdateSystem<FixedUpdate>(ref currentLoopSystem, 1))
            {
                Debug.LogError("Failed to insert FixedUpdate system to PlayerLoop.");
                return;
            }

            PlayerLoop.SetPlayerLoop(currentLoopSystem);

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeState;
            EditorApplication.playModeStateChanged += OnPlayModeState;

            static void OnPlayModeState(PlayModeStateChange state)
            {
                if (state != PlayModeStateChange.ExitingPlayMode) 
                    return;
                
                UnityEngine.LowLevel.PlayerLoopSystem currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
                RemoveTimeSystem<Update>(ref currentPlayerLoop);
                RemoveTimeSystem<FixedUpdate>(ref currentPlayerLoop);
                PlayerLoop.SetPlayerLoop(currentPlayerLoop);
                UpdateServiceManager.Clear();
                FixedUpdateServiceManager.Clear();
            }
#endif
        }

        private static bool InsertUpdateSystem<T>(ref UnityEngine.LowLevel.PlayerLoopSystem playerLoopSystem, int index)
        {
            _playerLoopSystem = new UnityEngine.LowLevel.PlayerLoopSystem
            {
                type = typeof(T),
                updateDelegate = Update,
                subSystemList = null
            };

            return PlayerLoopUtils.InsertSystem<T>(ref playerLoopSystem, in _playerLoopSystem, index);
        }
        
        private static bool InsertFixedUpdateSystem<T>(ref UnityEngine.LowLevel.PlayerLoopSystem playerLoopSystem, int index)
        {
            _playerLoopSystem = new UnityEngine.LowLevel.PlayerLoopSystem
            {
                type = typeof(T),
                updateDelegate = FixedUpdate,
                subSystemList = null
            };

            return PlayerLoopUtils.InsertSystem<T>(ref playerLoopSystem, in _playerLoopSystem, index);
        }

        private static void RemoveTimeSystem<T>(ref UnityEngine.LowLevel.PlayerLoopSystem playerLoopSystem) =>
            PlayerLoopUtils.RemoveSystem<T>(ref playerLoopSystem, in _playerLoopSystem);

        private static void Update()
        {
            UpdateServiceManager.UpdateTime();
        }
        
        private static void FixedUpdate()
        {
            FixedUpdateServiceManager.FixedUpdateTime();
        }
    }
}
