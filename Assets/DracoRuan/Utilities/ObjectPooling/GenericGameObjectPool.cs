using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class GameObjectPool<T> : IGameObjectPool where T : Component
    {
        private readonly ObjectPool<T> _objectPool;
        private readonly HashSet<int> _spawnedInstanceIds;

        public int PoolHashKey { get; }

        public GameObjectPool(T prefab, int defaultCapacity, int preloadCount)
        {
            this.PoolHashKey = prefab.gameObject.GetInstanceID();
            this._spawnedInstanceIds = new HashSet<int>(ObjectPoolConstant.PoolMaxSize);
            this._objectPool = this.CreateObjectPool(prefab, defaultCapacity, preloadCount);
        }

        private ObjectPool<T> CreateObjectPool(T prefab, int defaultCapacity, int preloadCount)
        {
            ObjectPool<T> objectPool = new ObjectPool<T>(
                createFunc: CreateInstance,
                actionOnGet: OnGetInstance,
                actionOnRelease: OnReleaseInstance,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: preloadCount);
            return objectPool;

            T CreateInstance()
            {
                T instance = Object.Instantiate(prefab);
                return instance;
            }

            void OnGetInstance(T instance)
            {
                instance.gameObject.SetActive(true);
            }

            void OnReleaseInstance(T instance)
            {
                instance.gameObject.SetActive(false);
            }

            void OnDestroyInstance(T instance)
            {
                Object.Destroy(instance.gameObject);
            }
        }

        public T Spawn()
        {
            T instance = this._objectPool.Get();
            int instanceId = instance.gameObject.GetInstanceID();
            this._spawnedInstanceIds.Add(instanceId);
            return instance;
        }

        public void Despawn(T instance)
        {
            int instanceId = instance.gameObject.GetInstanceID();
            this._objectPool.Release(instance);
            this._spawnedInstanceIds.Remove(instanceId);
        }
        
        public bool ContainInstance(T instance)
        {
            int instanceId = instance.gameObject.GetInstanceID();
            return this._spawnedInstanceIds.Contains(instanceId);
        }

        public void ClearPool()
        {
            this._objectPool.Clear();
            this._spawnedInstanceIds.Clear();
        }
    }
}