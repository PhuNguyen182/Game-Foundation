using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class GameObjectPool : IGameObjectPool
    {
        private readonly ObjectPool<GameObject> _objectPool;
        private readonly HashSet<int> _spawnedInstanceIds;
        
        public int PoolHashKey { get; }

        public GameObjectPool(GameObject prefab, int defaultCapacity, int preloadCount)
        {
            this.PoolHashKey = prefab.GetInstanceID();
            this._spawnedInstanceIds = new HashSet<int>(ObjectPoolConstant.PoolMaxSize);
            this._objectPool = this.CreateObjectPool(prefab, defaultCapacity, preloadCount);
        }

        private ObjectPool<GameObject> CreateObjectPool(GameObject prefab, int defaultCapacity, int preloadCount)
        {
            ObjectPool<GameObject> objectPool = new ObjectPool<GameObject>(
                createFunc: CreateInstance,
                actionOnGet: OnGetInstance,
                actionOnRelease: OnReleaseInstance,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: preloadCount);
            return objectPool;

            GameObject CreateInstance()
            {
                GameObject instance = Object.Instantiate(prefab);
                return instance;
            }

            void OnGetInstance(GameObject instance)
            {
                instance.SetActive(true);
            }

            void OnReleaseInstance(GameObject instance)
            {
                instance.SetActive(false);
            }

            void OnDestroyInstance(GameObject instance)
            {
                Object.Destroy(instance);
            }
        }
        
        public GameObject Spawn()
        {
            GameObject instance = this._objectPool.Get();
            int instanceId = instance.GetInstanceID();
            this._spawnedInstanceIds.Add(instanceId);
            return instance;
        }

        public void Despawn(GameObject instance)
        {
            int instanceId = instance.GetInstanceID();
            this._objectPool.Release(instance);
            this._spawnedInstanceIds.Remove(instanceId);
        }

        public bool ContainInstance(GameObject instance)
        {
            int instanceId = instance.GetInstanceID();
            return this._spawnedInstanceIds.Contains(instanceId);
        }

        public void ClearPool()
        {
            this._objectPool.Clear();
            this._spawnedInstanceIds.Clear();
        }
    }
}