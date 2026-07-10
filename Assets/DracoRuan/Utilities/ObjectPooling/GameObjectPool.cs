using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class GameObjectPool : IGameObjectPool, IDisposable
    {
        private readonly ObjectPool<GameObject> _objectPool;
#if UNITY_6000_0_OR_NEWER
        private readonly HashSet<EntityId> _spawnedInstanceIds;
#else
        private readonly HashSet<int> _spawnedInstanceIds;
#endif
        
        private bool _isDisposed;
        
#if UNITY_6000_0_OR_NEWER
        public EntityId PoolHashKey { get; }
#else
        public int PoolHashKey { get; }
#endif

        public GameObjectPool(GameObject prefab, int defaultCapacity, int preloadCount)
        {
#if UNITY_6000_0_OR_NEWER
            this.PoolHashKey = prefab.GetEntityId();
            this._spawnedInstanceIds = new HashSet<EntityId>(ObjectPoolConstant.PoolMaxSize);
#else
            this.PoolHashKey = prefab.GetInstanceID();
            this._spawnedInstanceIds = new HashSet<int>(ObjectPoolConstant.PoolMaxSize);
#endif
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

            void OnGetInstance(GameObject instance) => instance.SetActive(true);
            
            void OnReleaseInstance(GameObject instance) => instance.SetActive(false);
            
            void OnDestroyInstance(GameObject instance) => Object.Destroy(instance);
        }
        
        public GameObject Spawn()
        {
            GameObject instance = this._objectPool.Get();
#if UNITY_6000_0_OR_NEWER
            EntityId instanceId = instance.GetEntityId();
#else
            int instanceId = instance.GetInstanceID();
#endif
            this._spawnedInstanceIds.Add(instanceId);
            return instance;
        }

        public void Despawn(GameObject instance)
        {
#if UNITY_6000_0_OR_NEWER
            EntityId instanceId = instance.GetEntityId();
#else
            int instanceId = instance.GetInstanceID();
#endif
            this._objectPool.Release(instance);
            this._spawnedInstanceIds.Remove(instanceId);
        }

        public bool ContainInstance(GameObject instance)
        {
#if UNITY_6000_0_OR_NEWER
            EntityId instanceId = instance.GetEntityId();
#else
            int instanceId = instance.GetInstanceID();
#endif
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