using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class ObjectPoolSample
    {
        private readonly PoolableObject _poolableObject = new();
        
        private void Test()
        {
            PoolableObject poolableObject = ObjectPooling.Spawn(_poolableObject);
            Debug.Log(poolableObject.PoolHashKey);
        }
    }

    public class PoolableObject : MonoBehaviour, IPoolableObject
    {
#if UNITY_6000_0_OR_NEWER
        public EntityId PoolHashKey { get; set; }
#else
        public int PoolHashKey { get; set; }
#endif
        
        public void SetPoolHashKey(int hashKey)
        {
            this.PoolHashKey = hashKey;    
        }
    }
}