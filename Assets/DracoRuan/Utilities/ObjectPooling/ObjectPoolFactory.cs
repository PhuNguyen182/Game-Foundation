using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public static class ObjectPoolFactory
    {
        public static GameObjectPool<TPoolableObject> CreateObjectPool<TPoolableObject>(TPoolableObject prefab,
            int defaultCapacity, int preloadCount) where TPoolableObject : Component
        {
            GameObjectPool<TPoolableObject> objectPool =
                new GameObjectPool<TPoolableObject>(prefab, defaultCapacity, preloadCount);
            return objectPool;
        }

        public static GameObjectPool CreateObjectPool(GameObject prefab, int defaultCapacity, int preloadCount)
        {
            GameObjectPool objectPool = new GameObjectPool(prefab, defaultCapacity, preloadCount);
            return objectPool;
        }
    }
}
