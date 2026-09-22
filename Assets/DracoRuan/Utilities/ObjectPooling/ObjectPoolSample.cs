using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class ObjectPoolSample
    {
        private readonly PoolableObject _poolableObject = new();
        
        private void Test()
        {
            PoolableObject poolableObject = ObjectPool.Spawn(_poolableObject);
            Debug.Log(poolableObject.PoolHashKey);
        }
    }

    public class PoolableObject : MonoBehaviour, IPoolableObject
    {
        public EntityId PoolHashKey { get; set; }
        
        public void SetPoolHashKey(int hashKey)
        {
            this.PoolHashKey = hashKey;    
        }
    }
}