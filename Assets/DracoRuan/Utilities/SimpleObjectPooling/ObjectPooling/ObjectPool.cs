using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.TypeCreator;

namespace ObjectPooling
{
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool;
        private readonly int _maxCapacity;

        public int Count => _pool.Count;

        public ObjectPool(PoolConfig<T> config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.InitialCapacity < 0)
                throw new ArgumentException("Initial capacity cannot be negative", nameof(config));

            if (config.MaxCapacity < 0)
                throw new ArgumentException("Max capacity cannot be negative", nameof(config));

            this._maxCapacity = config.MaxCapacity;
            this._pool = new Stack<T>(config.InitialCapacity);
            
            for (int i = 0; i < config.InitialCapacity; i++)
                this._pool.Push(TypeFactory.Create<T>());
        }

        public T Get()
        {
            T instance = this._pool.Count > 0 ? this._pool.Pop() : TypeFactory.Create<T>();
            (instance as IPoolable)?.OnGet();
            return instance;
        }

        public void Release(T obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            
            (obj as IPoolable)?.OnRelease();
            if (this._maxCapacity == 0 || this._pool.Count < this._maxCapacity)
                this._pool.Push(obj);
        }

        public void Clear() => _pool.Clear();
    }
}
