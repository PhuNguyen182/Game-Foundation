using System.Collections.Generic;
using LoopSystem = UnityEngine.LowLevel.PlayerLoopSystem;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.Core
{
    public static class PlayerLoopUtils
    {
        /// <summary>
        /// Inserts <paramref name="systemToInsert"/> at <paramref name="index"/> in the subsystem list of the first
        /// system of type <typeparamref name="T"/> found anywhere in the tree. Returns false if there is none.
        /// </summary>
        public static bool InsertSystem<T>(ref LoopSystem playerLoopSystem,
            in LoopSystem systemToInsert,
            int index)
        {
            if (playerLoopSystem.type != typeof(T))
                return HandleSubSystemLoop<T>(ref playerLoopSystem, systemToInsert, index);

            List<LoopSystem> playerLoopSystemList = new();
            if (playerLoopSystem.subSystemList != null)
                playerLoopSystemList.AddRange(playerLoopSystem.subSystemList);

            playerLoopSystemList.Insert(index, systemToInsert);
            playerLoopSystem.subSystemList = playerLoopSystemList.ToArray();
            return true;
        }

        /// <summary>
        /// Inserts <paramref name="systemToInsert"/> directly before the <typeparamref name="TAnchor"/> child of the
        /// first <typeparamref name="TParent"/> system found anywhere in the tree, or at the start if that child does
        /// not exist (a Unity version that renamed it). Unlike a fixed index this keeps working when Unity adds
        /// entries. Returns false if there is no <typeparamref name="TParent"/>.
        /// </summary>
        public static bool InsertSystemBefore<TParent, TAnchor>(ref LoopSystem playerLoopSystem,
            in LoopSystem systemToInsert)
        {
            if (playerLoopSystem.type == typeof(TParent))
            {
                List<LoopSystem> children = new();
                if (playerLoopSystem.subSystemList != null)
                    children.AddRange(playerLoopSystem.subSystemList);

                int index = 0;
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i].type != typeof(TAnchor))
                        continue;

                    index = i;
                    break;
                }

                children.Insert(index, systemToInsert);
                playerLoopSystem.subSystemList = children.ToArray();
                return true;
            }

            if (playerLoopSystem.subSystemList == null)
                return false;

            for (int i = 0; i < playerLoopSystem.subSystemList.Length; i++)
            {
                if (InsertSystemBefore<TParent, TAnchor>(ref playerLoopSystem.subSystemList[i], in systemToInsert))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Removes every child equal to <paramref name="systemToRemove"/> (same type and update delegate) from the
        /// subsystem list of every system of type <typeparamref name="T"/> anywhere in the tree - the counterpart of
        /// <see cref="InsertSystem{T}"/>. Returns whether anything was removed; lists that do not change are left as
        /// they are.
        /// </summary>
        public static bool RemoveSystem<T>(ref LoopSystem playerLoopSystem, in LoopSystem systemToRemove)
        {
            if (playerLoopSystem.subSystemList == null)
                return false;

            bool removed = playerLoopSystem.type == typeof(T) && RemoveMatchingChildren(ref playerLoopSystem, in systemToRemove);

            for (int i = 0; i < playerLoopSystem.subSystemList.Length; i++)
                removed |= RemoveSystem<T>(ref playerLoopSystem.subSystemList[i], in systemToRemove);

            return removed;
        }

        private static bool RemoveMatchingChildren(ref LoopSystem parent, in LoopSystem target)
        {
            LoopSystem[] children = parent.subSystemList;

            int matches = 0;
            for (int i = 0; i < children.Length; i++)
            {
                if (IsSameSystem(in children[i], in target))
                    matches++;
            }

            if (matches == 0)
                return false;

            LoopSystem[] kept = new LoopSystem[children.Length - matches];
            int write = 0;
            for (int i = 0; i < children.Length; i++)
            {
                if (!IsSameSystem(in children[i], in target))
                    kept[write++] = children[i];
            }

            parent.subSystemList = kept;
            return true;
        }

        private static bool IsSameSystem(in LoopSystem a, in LoopSystem b) =>
            a.type == b.type && a.updateDelegate == b.updateDelegate;

        private static bool HandleSubSystemLoop<T>(ref LoopSystem playerLoopSystem,
            in LoopSystem systemToInsert, int index)
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
