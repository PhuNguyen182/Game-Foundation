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
        private readonly HashSet<int> _spawnedInstanceIds;

        private bool _isDisposed;

        public int PoolHashKey { get; }

        public GameObjectPool(TPoolableObject prefab, int defaultCapacity, int preloadCount)
        {
            this.PoolHashKey = prefab.gameObject.GetInstanceID();
            this._spawnedInstanceIds = new HashSet<int>(ObjectPoolConstant.PoolMaxSize);
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
            int instanceId = instance.gameObject.GetInstanceID();
            this._spawnedInstanceIds.Add(instanceId);
            return instance;
        }

        public void Despawn(TPoolableObject instance)
        {
            int instanceId = instance.gameObject.GetInstanceID();
            this._objectPool.Release(instance);
            this._spawnedInstanceIds.Remove(instanceId);
        }

        public bool ContainInstance(TPoolableObject instance)
        {
            int instanceId = instance.gameObject.GetInstanceID();
            return this._spawnedInstanceIds.Contains(instanceId);
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