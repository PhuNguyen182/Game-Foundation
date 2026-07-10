using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public interface IGameObjectPool
    {
#if UNITY_6000_0_OR_NEWER
        public EntityId PoolHashKey { get; }
#else
        public int PoolHashKey { get; }
#endif
    }
}