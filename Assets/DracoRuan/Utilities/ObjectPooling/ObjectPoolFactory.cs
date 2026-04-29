using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public static class ObjectPoolFactory
    {
        public static GameObjectPool<T> CreateObjectPool<T>(T prefab, int defaultCapacity, int preloadCount)
            where T : Component
        {
            GameObjectPool<T> objectPool = new GameObjectPool<T>(prefab, defaultCapacity, preloadCount);
            return objectPool;
        }

        public static GameObjectPool CreateObjectPool(GameObject prefab, int defaultCapacity, int preloadCount)
        {
            GameObjectPool objectPool = new GameObjectPool(prefab, defaultCapacity, preloadCount);
            return objectPool;
        }
    }
}
