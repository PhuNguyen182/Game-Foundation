using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public static class ObjectPoolManager<TPoolableObject> where TPoolableObject : Component
    {
        private static readonly Dictionary<int, GameObjectPool<TPoolableObject>> ObjectPools = new();

        public static void PreloadPool(TPoolableObject prefab, int defaultCapacity = ObjectPoolConstant.PoolCapacity,
            int preloadCount = ObjectPoolConstant.PoolMaxSize)
        {
            int hashId = prefab.gameObject.GetInstanceID();
            if (ObjectPools.ContainsKey(hashId))
                return;

            GameObjectPool<TPoolableObject> objectPool =
                ObjectPoolFactory.CreateObjectPool(prefab, defaultCapacity, preloadCount);
            ObjectPools.Add(objectPool.PoolHashKey, objectPool);
        }

        public static TPoolableObject Spawn(TPoolableObject prefab)
        {
            TPoolableObject instance;
            int hashId = prefab.gameObject.GetInstanceID();
            if (ObjectPools.TryGetValue(hashId, out GameObjectPool<TPoolableObject> objectPool))
            {
                instance = objectPool.Spawn();
            }
            else
            {
                PreloadPool(prefab);
                GameObjectPool<TPoolableObject> createdObjectPool = ObjectPools[hashId];
                instance = createdObjectPool.Spawn();
            }

            return instance;
        }

        public static TPoolableObject Spawn(TPoolableObject prefab, Transform parent)
        {
            TPoolableObject instance = Spawn(prefab);
            instance.transform.SetParent(parent);
            return instance;
        }

        public static TPoolableObject Spawn(TPoolableObject prefab, Vector3 position)
        {
            TPoolableObject instance = Spawn(prefab);
            instance.transform.position = position;
            return instance;
        }

        public static TPoolableObject Spawn(TPoolableObject prefab, Vector3 position, Transform parent)
        {
            TPoolableObject instance = Spawn(prefab);
            instance.transform.position = position;
            instance.transform.SetParent(parent);
            return instance;
        }

        public static TPoolableObject Spawn(TPoolableObject prefab, Vector3 position, Quaternion rotation)
        {
            TPoolableObject instance = Spawn(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        public static TPoolableObject Spawn(TPoolableObject prefab, Vector3 position, Quaternion rotation,
            Transform parent)
        {
            TPoolableObject instance = Spawn(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.SetParent(parent);
            return instance;
        }

        public static void Despawn(TPoolableObject instance)
        {
            foreach (var kvp in ObjectPools)
            {
                if (!kvp.Value.ContainInstance(instance))
                    continue;

                GameObjectPool<TPoolableObject> objectPool = kvp.Value;
                objectPool.Despawn(instance);
                return;
            }

            Debug.Log($"This Object {instance.name} with instance id {instance.gameObject.GetInstanceID()} has not been spawned in any object pool. Destroy it instead!");
            Object.Destroy(instance);
        }

        public static void ClearObjectPool(TPoolableObject originalPrefab)
        {
            int instanceId = originalPrefab.gameObject.GetInstanceID();
            if (!ObjectPools.TryGetValue(instanceId, out GameObjectPool<TPoolableObject> objectPool))
                return;

            objectPool.Dispose();
            ObjectPools.Remove(instanceId);
        }
    }
}