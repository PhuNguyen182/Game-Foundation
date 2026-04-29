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

        #endregion
        
        #region Generic Object Pooling
        
        public static void PreloadPool<T>(T prefab, int defaultCapacity = ObjectPoolConstant.PoolCapacity, 
            int preloadCount = ObjectPoolConstant.PoolMaxSize) where T : Component
        {
            ObjectPoolManager<T>.PreloadPool(prefab, defaultCapacity, preloadCount);
        }
        
        public static T Spawn<T>(T prefab) where T : Component
        {
            return ObjectPoolManager<T>.Spawn(prefab);
        }

        public static T Spawn<T>(T prefab, Transform parent) where T : Component
        {
            return ObjectPoolManager<T>.Spawn(prefab, parent);
        }
        
        public static T Spawn<T>(T prefab, Vector3 position) where T : Component
        {
            return ObjectPoolManager<T>.Spawn(prefab, position);
        }
        
        public static T Spawn<T>(T prefab, Vector3 position, Transform parent) where T : Component
        {
            return ObjectPoolManager<T>.Spawn(prefab, position, parent);
        }
        
        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            return ObjectPoolManager<T>.Spawn(prefab, position, rotation);
        }
        
        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent) where T : Component
        {
            return ObjectPoolManager<T>.Spawn(prefab, position, rotation, parent);
        }

        public static void Despawn<T>(T instance) where T : Component
        {
            ObjectPoolManager<T>.Despawn(instance);
        }
        
        #endregion
    }
}