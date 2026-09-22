using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public interface IPoolableObject
    {
        public EntityId PoolHashKey { get; set; }
    }
}