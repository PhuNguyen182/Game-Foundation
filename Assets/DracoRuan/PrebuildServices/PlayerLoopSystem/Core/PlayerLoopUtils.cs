using System.Collections.Generic;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.Core
{
    public static class PlayerLoopUtils
    {
        public static bool InsertSystem<T>(ref UnityEngine.LowLevel.PlayerLoopSystem playerLoopSystem,
            in UnityEngine.LowLevel.PlayerLoopSystem systemToInsert,
            int index)
        {
            if (playerLoopSystem.type != typeof(T))
                return HandleSubSystemLoop<T>(ref playerLoopSystem, systemToInsert, index);

            List<UnityEngine.LowLevel.PlayerLoopSystem> playerLoopSystemList = new();
            if (playerLoopSystem.subSystemList != null)
                playerLoopSystemList.AddRange(playerLoopSystem.subSystemList);

            playerLoopSystemList.Insert(index, systemToInsert);
            playerLoopSystem.subSystemList = playerLoopSystemList.ToArray();
            return true;
        }

        public static void RemoveSystem<T>(ref UnityEngine.LowLevel.PlayerLoopSystem playerLoopSystem,
            in UnityEngine.LowLevel.PlayerLoopSystem systemToRemove)
        {
            if (playerLoopSystem.subSystemList == null)
                return;

            List<UnityEngine.LowLevel.PlayerLoopSystem> playerLoopSystems = new(playerLoopSystem.subSystemList);
            for (int i = 0; i < playerLoopSystems.Count; i++)
            {
                if (playerLoopSystems[i].type == systemToRemove.type &&
                    playerLoopSystems[i].updateDelegate == systemToRemove.updateDelegate)
                {
                    playerLoopSystems.RemoveAt(i);
                    playerLoopSystem.subSystemList = playerLoopSystems.ToArray();
                    return;
                }
            }
        }

        private static bool HandleSubSystemLoop<T>(ref UnityEngine.LowLevel.PlayerLoopSystem playerLoopSystem,
            in UnityEngine.LowLevel.PlayerLoopSystem systemToInsert, int index)
        {
            if (playerLoopSystem.subSystemList == null)
                return false;

            for (int i = 0; i < playerLoopSystem.subSystemList.Length; ++i)
            {
                if (!InsertSystem<T>(ref playerLoopSystem.subSystemList[i], in systemToInsert, index))
                    continue;

                return true;
            }

            return false;
        }
    }
}