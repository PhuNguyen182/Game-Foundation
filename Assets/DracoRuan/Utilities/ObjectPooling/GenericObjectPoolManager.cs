using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public static class ObjectPoolManager<T> where T : Component
    {
        private static readonly Dictionary<int, GameObjectPool<T>> ObjectPools = new();

        public static void PreloadPool(T prefab, int defaultCapacity = ObjectPoolConstant.PoolCapacity, 
            int preloadCount = ObjectPoolConstant.PoolMaxSize)
        {
            int hashId = prefab.gameObject.GetInstanceID();
            if (ObjectPools.ContainsKey(hashId)) 
                return;
            
            GameObjectPool<T> objectPool = ObjectPoolFactory.CreateObjectPool(prefab, defaultCapacity, preloadCount);
            ObjectPools.Add(objectPool.PoolHashKey, objectPool);
        }

        public static T Spawn(T prefab)
        {
            T instance;
            int hashId = prefab.gameObject.GetInstanceID();
            if (ObjectPools.TryGetValue(hashId, out GameObjectPool<T> objectPool))
            {
                instance = objectPool.Spawn();
            }
            else
            {
                PreloadPool(prefab);
                GameObjectPool<T> createdObjectPool = ObjectPools[hashId];
                instance = createdObjectPool.Spawn();
            }
            
            return instance;
        }

        public static T Spawn(T prefab, Transform parent)
        {
            T instance = Spawn(prefab);
            instance.transform.SetParent(parent);
            return instance;
        }
        
        public static T Spawn(T prefab, Vector3 position)
        {
            T instance = Spawn(prefab);
            instance.transform.position = position;
            return instance;
        }
        
        public static T Spawn(T prefab, Vector3 position, Transform parent)
        {
            T instance = Spawn(prefab);
            instance.transform.position = position;
            instance.transform.SetParent(parent);
            return instance;
        }
        
        public static T Spawn(T prefab, Vector3 position, Quaternion rotation)
        {
            T instance = Spawn(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }
        
        public static T Spawn(T prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            T instance = Spawn(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.SetParent(parent);
            return instance;
        }

        public static void Despawn(T instance)
        {
            foreach (var kvp in ObjectPools)
            {
                if (!kvp.Value.ContainInstance(instance)) 
                    continue;
                
                GameObjectPool<T> objectPool = kvp.Value;
                objectPool.Despawn(instance);
                return;
            }

            Debug.Log($"This Object {instance.name} with instance id {instance.gameObject.GetInstanceID()} has not been spawned in any object pool.");
            Object.Destroy(instance);
        }

        public static void ClearPool()
        {
            foreach (var kvp in ObjectPools)
                kvp.Value.ClearPool();
            
            ObjectPools.Clear();
        }
    }
}