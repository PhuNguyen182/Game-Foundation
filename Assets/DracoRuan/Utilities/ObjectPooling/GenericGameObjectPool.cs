using System;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class GameObjectPool<TPoolableObject> : IGameObjectPool, IDisposable
        where TPoolableObject : Component, IPoolableObject
    {
        private readonly ObjectPool<TPoolableObject> _objectPool;

        private bool _isDisposed;

#if UNITY_6000_0_OR_NEWER
        public EntityId PoolHashKey { get; }
#else
        public int PoolHashKey { get; }
#endif

        public GameObjectPool(TPoolableObject prefab, int defaultCapacity, int maxSize)
        {
#if UNITY_6000_0_OR_NEWER
            this.PoolHashKey = prefab.gameObject.GetEntityId();
#else
            this.PoolHashKey = prefab.gameObject.GetInstanceID();
#endif
            this._objectPool = this.CreateObjectPool(prefab, defaultCapacity, maxSize);
        }

        private ObjectPool<TPoolableObject> CreateObjectPool(TPoolableObject prefab, int defaultCapacity, int maxSize)
        {
            ObjectPool<TPoolableObject> objectPool = new ObjectPool<TPoolableObject>(
                createFunc: CreateInstance,
                actionOnGet: OnGetInstance,
                actionOnRelease: OnReleaseInstance,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
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
            instance.PoolHashKey = this.PoolHashKey;
            return instance;
        }

        public void Despawn(TPoolableObject instance)
        {
            this._objectPool.Release(instance);
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