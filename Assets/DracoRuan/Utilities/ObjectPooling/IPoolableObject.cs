using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public interface IPoolableObject
    {
#if UNITY_6000_0_OR_NEWER
        public EntityId PoolHashKey { get; set; }
#else
        public int PoolHashKey { get; set; }
#endif
    }
}