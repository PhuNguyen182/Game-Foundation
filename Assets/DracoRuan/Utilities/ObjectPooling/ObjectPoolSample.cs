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

    public class PoolableObject : MonoBehaviour
    {
        public int PoolHashKey { get; private set; }
        
        public void SetPoolHashKey(int hashKey)
        {
            this.PoolHashKey = hashKey;    
        }
    }
}