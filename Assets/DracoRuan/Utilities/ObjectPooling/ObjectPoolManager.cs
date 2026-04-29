using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public static class ObjectPoolManager
    {
        private static readonly Dictionary<int, GameObjectPool> ObjectPools = new();

        public static void PreloadPool(GameObject prefab, int defaultCapacity = ObjectPoolConstant.PoolCapacity,
            int preloadCount = ObjectPoolConstant.PoolMaxSize)
        {
            int hashId = prefab.GetInstanceID();
            if (ObjectPools.ContainsKey(hashId))
                return;

            GameObjectPool objectPool = ObjectPoolFactory.CreateObjectPool(prefab, defaultCapacity, preloadCount);
            ObjectPools.Add(objectPool.PoolHashKey, objectPool);
        }

        public static GameObject Spawn(GameObject prefab)
        {
            GameObject instance;
            int hashId = prefab.GetInstanceID();
            if (ObjectPools.TryGetValue(hashId, out GameObjectPool objectPool))
            {
                instance = objectPool.Spawn();
            }
            else
            {
                PreloadPool(prefab);
                GameObjectPool createdObjectPool = ObjectPools[hashId];
                instance = createdObjectPool.Spawn();
            }

            return instance;
        }

        public static GameObject Spawn(GameObject prefab, Transform parent)
        {
            GameObject instance = Spawn(prefab);
            instance.transform.SetParent(parent);
            return instance;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position)
        {
            GameObject instance = Spawn(prefab);
            instance.transform.position = position;
            return instance;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Transform parent)
        {
            GameObject instance = Spawn(prefab);
            instance.transform.position = position;
            instance.transform.SetParent(parent);
            return instance;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject instance = Spawn(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject instance = Spawn(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.SetParent(parent);
            return instance;
        }

        public static void Despawn(GameObject instance)
        {
            foreach (var kvp in ObjectPools)
            {
                if (!kvp.Value.ContainInstance(instance))
                    continue;

                GameObjectPool objectPool = kvp.Value;
                objectPool.Despawn(instance);
                return;
            }

            Debug.Log($"This Object {instance.name} with instance id {instance.GetInstanceID()} has not been spawned in any object pool. Destroy it instead!");
            Object.Destroy(instance);
        }

        public static void ClearObjectPool(GameObject originalPrefab)
        {
            int instanceId = originalPrefab.GetInstanceID();
            if (!ObjectPools.TryGetValue(instanceId, out GameObjectPool objectPool))
                return;

            objectPool.Dispose();
            ObjectPools.Remove(instanceId);
        }
    }
}