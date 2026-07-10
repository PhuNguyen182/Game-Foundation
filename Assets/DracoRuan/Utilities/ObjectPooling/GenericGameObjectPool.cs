using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class GameObjectPool<TPoolableObject> : IGameObjectPool, IDisposable where TPoolableObject : Component
    {
        private readonly ObjectPool<TPoolableObject> _objectPool;
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

        public GameObjectPool(TPoolableObject prefab, int defaultCapacity, int preloadCount)
        {
#if UNITY_6000_0_OR_NEWER
            this._spawnedInstanceIds = new HashSet<EntityId>(ObjectPoolConstant.PoolMaxSize);
            this.PoolHashKey = prefab.gameObject.GetEntityId();
#else
            this.PoolHashKey = prefab.gameObject.GetInstanceID();
            this._spawnedInstanceIds = new HashSet<int>(ObjectPoolConstant.PoolMaxSize);
#endif
            this._objectPool = this.CreateObjectPool(prefab, defaultCapacity, preloadCount);
        }

        private ObjectPool<TPoolableObject> CreateObjectPool(TPoolableObject prefab, int defaultCapacity,
            int preloadCount)
        {
            ObjectPool<TPoolableObject> objectPool = new ObjectPool<TPoolableObject>(
                createFunc: CreateInstance,
                actionOnGet: OnGetInstance,
                actionOnRelease: OnReleaseInstance,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: preloadCount);
            return objectPool;

            TPoolableObject CreateInstance()
            {
                TPoolableObject instance = Object.Instantiate(prefab);
                return instance;
            }

            void OnGetInstance(TPoolableObject instance) => instance.gameObject.SetActive(true);

            void OnReleaseInstance(TPoolableObject instance) => instance.gameObject.SetActive(false);

            void OnDestroyInstance(TPoolableObject instance) => Object.Destroy(instance.gameObject);
        }

        public TPoolableObject Spawn()
        {
            TPoolableObject instance = this._objectPool.Get();
#if UNITY_6000_0_OR_NEWER
            EntityId instanceId = instance.gameObject.GetEntityId();
#else
            int instanceId = instance.gameObject.GetInstanceID();
#endif
            this._spawnedInstanceIds.Add(instanceId);
            return instance;
        }

        public void Despawn(TPoolableObject instance)
        {
#if UNITY_6000_0_OR_NEWER
            EntityId instanceId = instance.gameObject.GetEntityId();
#else
            int instanceId = instance.gameObject.GetInstanceID();
#endif
            this._objectPool.Release(instance);
            this._spawnedInstanceIds.Remove(instanceId);
        }

        public bool ContainInstance(TPoolableObject instance)
        {
#if UNITY_6000_0_OR_NEWER
            EntityId instanceId = instance.gameObject.GetEntityId();
#else
            int instanceId = instance.gameObject.GetInstanceID();
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