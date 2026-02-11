using System;
using System.Collections.Generic;

namespace ObjectPooling
{
    public static class ObjectPoolManager
    {
        private static readonly Dictionary<Type, object> Pools = new();
        
        public static T Get<T>() where T : class, new()
        {
            return GetPool<T>().Get();
        }
        
        public static void Release<T>(T obj) where T : class, new()
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            GetPool<T>().Release(obj);
        }
        
        public static void CreatePool<T>(PoolConfig<T> config) where T : class, new()
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            Type type = typeof(T);
            Pools[type] = new ObjectPool<T>(config);
        }
        
        public static void ClearPool<T>() where T : class, new()
        {
            Type type = typeof(T);
            if (Pools.TryGetValue(type, out object pool))
            {
                ((ObjectPool<T>)pool).Clear();
            }
        }
        
        public static void RemovePool<T>() where T : class, new()
        {
            Type type = typeof(T);
            Pools.Remove(type);
        }
        
        public static void ClearAllPools()
        {
            foreach (var pool in Pools.Values)
            {
                pool.GetType().GetMethod("Clear")?.Invoke(pool, null);
            }
        }
        
        public static void RemoveAllPools()
        {
            Pools.Clear();
        }
        
        public static int GetPoolCount<T>() where T : class, new()
        {
            Type type = typeof(T);
            return Pools.TryGetValue(type, out object pool) ? ((ObjectPool<T>)pool).Count : 0;
        }
        
        private static ObjectPool<T> GetPool<T>() where T : class, new()
        {
            Type type = typeof(T);
            if (Pools.TryGetValue(type, out object pool)) 
                return (ObjectPool<T>)pool;
            
            var config = new PoolConfig<T>
            {
                InitialCapacity = 10,
                MaxCapacity = 0,
            };
                
            pool = new ObjectPool<T>(config);
            Pools[type] = pool;

            return (ObjectPool<T>)pool;
        }
    }
}
