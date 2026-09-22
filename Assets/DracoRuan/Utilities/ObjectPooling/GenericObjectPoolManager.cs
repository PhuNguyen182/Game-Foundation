using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public static class ObjectPoolManager<TPoolableObject> where TPoolableObject : Component, IPoolableObject
    {
        private static readonly Dictionary<EntityId, GameObjectPool<TPoolableObject>> ObjectPools = new();

        public static void PreloadObjectPool(TPoolableObject prefab, int defaultCapacity = ObjectPoolConstant.PoolCapacity,
            int preloadCount = ObjectPoolConstant.PoolMaxSize)
        {
            EntityId hashId = prefab.gameObject.GetEntityId();
            if (ObjectPools.ContainsKey(hashId))
                return;

            GameObjectPool<TPoolableObject> objectPool =
                ObjectPoolFactory.CreateObjectPool(prefab, defaultCapacity, preloadCount);
            ObjectPools.Add(objectPool.PoolHashKey, objectPool);
        }

        public static TPoolableObject Spawn(TPoolableObject prefab)
        {
            TPoolableObject instance;
            EntityId hashId = prefab.gameObject.GetEntityId();
            if (ObjectPools.TryGetValue(hashId, out GameObjectPool<TPoolableObject> objectPool))
            {
                instance = objectPool.Spawn();
            }
            else
            {
                PreloadObjectPool(prefab);
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
            var poolHashKey = instance.PoolHashKey;
            if (ObjectPools.TryGetValue(poolHashKey, out GameObjectPool<TPoolableObject> objectPool))
            {
                objectPool.Despawn(instance);
                return;
            }
            
            EntityId hashId = instance.gameObject.GetEntityId();
            Debug.Log($"This Object {instance.name} with instance id {hashId} has not been spawned in any object pool. Destroy it instead!");
            Object.Destroy(instance);
        }

        public static void ClearObjectPool(TPoolableObject originalPrefab)
        {
            EntityId instanceId = originalPrefab.gameObject.GetEntityId();
            if (!ObjectPools.TryGetValue(instanceId, out GameObjectPool<TPoolableObject> objectPool))
                return;

            objectPool.Dispose();
            ObjectPools.Remove(instanceId);
        }
    }
}