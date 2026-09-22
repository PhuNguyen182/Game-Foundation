using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class GameObjectPool : IDisposable
    {
        private readonly ObjectPool<GameObject> _objectPool;
        private readonly HashSet<EntityId> _spawnedInstanceIds;
        
        private bool _isDisposed;
        
        public EntityId PoolHashKey { get; }

        public GameObjectPool(GameObject prefab, int defaultCapacity, int maxSize)
        {
            this.PoolHashKey = prefab.GetEntityId();
            this._spawnedInstanceIds = new HashSet<EntityId>(ObjectPoolConstant.PoolMaxSize);
            this._objectPool = this.CreateObjectPool(prefab, defaultCapacity, maxSize);
        }

        private ObjectPool<GameObject> CreateObjectPool(GameObject prefab, int defaultCapacity, int maxSize)
        {
            ObjectPool<GameObject> objectPool = new ObjectPool<GameObject>(
                createFunc: CreateInstance,
                actionOnGet: OnGetInstance,
                actionOnRelease: OnReleaseInstance,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
            return objectPool;

            GameObject CreateInstance()
            {
                GameObject instance = Object.Instantiate(prefab);
                return instance;
            }

            void OnGetInstance(GameObject instance) => instance.SetActive(true);
            
            void OnReleaseInstance(GameObject instance) => instance.SetActive(false);
            
            void OnDestroyInstance(GameObject instance) => Object.Destroy(instance);
        }
        
        public GameObject Spawn()
        {
            GameObject instance = this._objectPool.Get();
            EntityId instanceId = instance.GetEntityId();
            this._spawnedInstanceIds.Add(instanceId);
            return instance;
        }

        public void Despawn(GameObject instance)
        {
            EntityId instanceId = instance.GetEntityId();
            this._objectPool.Release(instance);
            this._spawnedInstanceIds.Remove(instanceId);
        }

        public bool ContainInstance(GameObject instance)
        {
            EntityId instanceId = instance.GetEntityId();
            bool containsInstance = this._spawnedInstanceIds.Contains(instanceId);
            return containsInstance;
        }

        private void ReleaseUnmanagedResources()
        {
            
        }

        private void Dispose(bool disposing)
        {
            if (this._isDisposed)
                return;
            
            this.ReleaseUnmanagedResources();
            if (disposing)
            {
                this._objectPool?.Dispose();
                this._spawnedInstanceIds.Clear();
            }
            
            this._isDisposed = true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~GameObjectPool()
        {
            this.Dispose(false);
        }
    }
}