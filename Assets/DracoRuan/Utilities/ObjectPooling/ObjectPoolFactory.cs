using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public static class ObjectPoolFactory
    {
        public static GameObjectPool<TPoolableObject> CreateObjectPool<TPoolableObject>(TPoolableObject prefab,
            int defaultCapacity, int maxSize) where TPoolableObject : Component, IPoolableObject
        {
            GameObjectPool<TPoolableObject> objectPool =
                new GameObjectPool<TPoolableObject>(prefab, defaultCapacity, maxSize);
            return objectPool;
        }

        public static GameObjectPool CreateObjectPool(GameObject prefab, int defaultCapacity, int maxSize)
        {
            GameObjectPool objectPool = new GameObjectPool(prefab, defaultCapacity, maxSize);
            return objectPool;
        }
    }
}
