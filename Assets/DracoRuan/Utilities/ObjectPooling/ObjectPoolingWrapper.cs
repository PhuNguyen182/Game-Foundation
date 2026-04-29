using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public static class ObjectPooling
    {
        #region Normal Game Object Pooling

        public static void PreloadPool(GameObject prefab, int defaultCapacity = ObjectPoolConstant.PoolCapacity,
            int preloadCount = ObjectPoolConstant.PoolMaxSize)
        {
            ObjectPoolManager.PreloadPool(prefab, defaultCapacity, preloadCount);
        }

        public static GameObject Spawn(GameObject prefab)
        {
            return ObjectPoolManager.Spawn(prefab);
        }

        public static GameObject Spawn(GameObject prefab, Transform parent)
        {
            return ObjectPoolManager.Spawn(prefab, parent);
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position)
        {
            return ObjectPoolManager.Spawn(prefab, position);
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Transform parent)
        {
            return ObjectPoolManager.Spawn(prefab, position, parent);
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return ObjectPoolManager.Spawn(prefab, position, rotation);
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            return ObjectPoolManager.Spawn(prefab, position, rotation, parent);
        }

        public static void Despawn(GameObject instance)
        {
            ObjectPoolManager.Despawn(instance);
        }

        public static void ClearObjectPool(GameObject originalPrefab)
        {
            ObjectPoolManager.ClearObjectPool(originalPrefab);
        }

        #endregion

        #region Generic Object Pooling

        public static void PreloadPool<TPoolableObject>(TPoolableObject prefab,
            int defaultCapacity = ObjectPoolConstant.PoolCapacity,
            int preloadCount = ObjectPoolConstant.PoolMaxSize) where TPoolableObject : Component
        {
            ObjectPoolManager<TPoolableObject>.PreloadPool(prefab, defaultCapacity, preloadCount);
        }

        public static TPoolableObject Spawn<TPoolableObject>(TPoolableObject prefab) where TPoolableObject : Component
        {
            return ObjectPoolManager<TPoolableObject>.Spawn(prefab);
        }

        public static TPoolableObject Spawn<TPoolableObject>(TPoolableObject prefab, Transform parent)
            where TPoolableObject : Component
        {
            return ObjectPoolManager<TPoolableObject>.Spawn(prefab, parent);
        }

        public static TPoolableObject Spawn<TPoolableObject>(TPoolableObject prefab, Vector3 position)
            where TPoolableObject : Component
        {
            return ObjectPoolManager<TPoolableObject>.Spawn(prefab, position);
        }

        public static TPoolableObject Spawn<TPoolableObject>(TPoolableObject prefab, Vector3 position, Transform parent)
            where TPoolableObject : Component
        {
            return ObjectPoolManager<TPoolableObject>.Spawn(prefab, position, parent);
        }

        public static TPoolableObject Spawn<TPoolableObject>(TPoolableObject prefab, Vector3 position,
            Quaternion rotation) where TPoolableObject : Component
        {
            return ObjectPoolManager<TPoolableObject>.Spawn(prefab, position, rotation);
        }

        public static TPoolableObject Spawn<TPoolableObject>(TPoolableObject prefab, Vector3 position,
            Quaternion rotation, Transform parent) where TPoolableObject : Component
        {
            return ObjectPoolManager<TPoolableObject>.Spawn(prefab, position, rotation, parent);
        }

        public static void Despawn<TPoolableObject>(TPoolableObject instance) where TPoolableObject : Component
        {
            ObjectPoolManager<TPoolableObject>.Despawn(instance);
        }

        public static void ClearObjectPool<TPoolableObject>(TPoolableObject originalPrefab)
            where TPoolableObject : Component
        {
            ObjectPoolManager<TPoolableObject>.ClearObjectPool(originalPrefab);
        }

        #endregion
    }
}