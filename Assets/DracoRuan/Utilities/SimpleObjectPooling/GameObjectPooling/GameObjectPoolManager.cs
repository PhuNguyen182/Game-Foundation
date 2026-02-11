using System.Collections.Generic;
using UnityEngine;

public static class GameObjectPoolManager
{
    private const int DefaultPoolSize = 30;
    private static readonly Dictionary<int, BaseGameObjectPool> Pools = new();

    #region Initialization
    
    private static void InitializeObjectPools(GameObject prefab = null, int quantity = DefaultPoolSize)
    {
        if (!prefab) 
            return;
        
        int prefabID = prefab.GetInstanceID();
        if (!Pools.ContainsKey(prefabID))
            Pools[prefabID] = new SimpleGameObjectPool(prefab, quantity);
    }
    
    private static void InitializeObjectPools<T>(T prefab = null, int quantity = DefaultPoolSize) where T : Component
    {
        if (!prefab) 
            return;
        
        int prefabID = prefab.GetInstanceID();
        if (!Pools.ContainsKey(prefabID))
            Pools[prefabID] = new GenericGameObjectPool<T>(prefab, quantity);
    }
    
    #endregion

    #region Preloading
    
    public static void PoolPreLoad(GameObject prefab, int quantity, Transform newParent = null)
    {
        InitializeObjectPools(prefab, 1);
        Pools[prefab.GetInstanceID()].Preload(quantity, newParent);
    }
    
    public static void PoolPreLoad<T>(T prefab, int quantity, Transform newParent = null) where T : Component
    {
        InitializeObjectPools(prefab, 1);
        Pools[prefab.GetInstanceID()].Preload(quantity, newParent);
    }
    
    public static GameObject[] Preload(GameObject prefab, int quantity = 1, Transform newParent = null)
    {
        InitializeObjectPools(prefab, quantity);
        GameObject[] gameObjects = new GameObject[quantity];
        
        for (int i = 0; i < quantity; i++)
            gameObjects[i] = Spawn(prefab, Vector3.zero, Quaternion.identity, newParent);
        
        for (int i = 0; i < quantity; i++)
            Despawn(gameObjects[i]);

        return gameObjects;
    }

    #endregion

    #region Spawning
    
    public static GameObject Spawn(GameObject prefab, string tag, Vector3 position, Quaternion rotation)
    {
        InitializeObjectPools(prefab);
        BaseGameObjectPool pool = Pools[prefab.GetInstanceID()];
        if (pool is not SimpleGameObjectPool simplePool) 
            return null;
        
        GameObject bullet = simplePool.Spawn(position, rotation, null, true);
        bullet.tag = tag;
        return bullet;

    }

    public static GameObject SpawnInstance(GameObject prefab, Vector3 position, Quaternion rotation,
        Transform parent = null,
        bool worldPositionStay = true)
    {
        InitializeObjectPools(prefab);
        BaseGameObjectPool pool = Pools[prefab.GetInstanceID()];
        if (pool is not SimpleGameObjectPool simplePool) 
            return null;
        
        GameObject bullet = simplePool.Spawn(position, rotation, parent, worldPositionStay);
        return bullet;
    }

    public static GameObject Spawn(GameObject prefab) => Spawn(prefab, Vector3.zero, Quaternion.identity, null);

    public static T Spawn<T>(T prefab) where T : Component => Spawn(prefab, Vector3.zero, Quaternion.identity);

    public static T Spawn<T>(T prefab, string tag, Vector3 position = default, Quaternion rotation = default) where T : Component
    {
        InitializeObjectPools(prefab);
        BaseGameObjectPool pool = Pools[prefab.gameObject.GetInstanceID()];
        if (pool is not GenericGameObjectPool<T> genericPool) 
            return null;
        
        T bullet = genericPool.Spawn(position, rotation, null, true);
        bullet.tag = tag;
        return bullet;
    }

    public static T SpawnInstance<T>(T prefab, Vector3 position = default, Quaternion rotation = default, 
        Transform parent = null, bool worldPositionStay = true) where T : Component
    {
        InitializeObjectPools(prefab);
        BaseGameObjectPool pool = Pools[prefab.gameObject.GetInstanceID()];
        if (pool is not GenericGameObjectPool<T> genericPool) 
            return null;
        
        T bullet = genericPool.Spawn(position, rotation, parent, worldPositionStay);
        return bullet;
    }

    private static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        InitializeObjectPools(prefab);
        BaseGameObjectPool pool = Pools[prefab.GetInstanceID()];
        if (pool is not SimpleGameObjectPool simplePool) 
            return null;
        
        return simplePool.Spawn(position, rotation, parent, true);
    }

    private static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        InitializeObjectPools(prefab);
        BaseGameObjectPool pool = Pools[prefab.gameObject.GetInstanceID()];
        if (pool is not GenericGameObjectPool<T> genericPool) 
            return null;
        
        return genericPool.Spawn(position, rotation, null, true);
    }

    #endregion

    #region Despawning
    
    public static void Despawn(GameObject gameObject, Transform parent, bool worldPositionStay = true)
    {
        BaseGameObjectPool gameObjectPool = null;
        foreach (var pool in Pools.Values)
        {
            if (!pool.ContainsInstance(gameObject.GetInstanceID())) 
                continue;
            
            gameObjectPool = pool;
            break;
        }

        if (gameObjectPool == null)
        {
            Debug.Log($"Object {gameObject.name} wasn't spawned from a pool. Destroying it instead.");
            Object.Destroy(gameObject);
        }
        else
        {
            gameObject.transform.SetParent(parent, worldPositionStay);
            gameObjectPool.Despawn(gameObject);
        }
    }

    public static void Despawn(GameObject gameObject)
    {
        BaseGameObjectPool gameObjectPool = null;
        foreach (var pool in Pools.Values)
        {
            if (!pool.ContainsInstance(gameObject.GetInstanceID())) 
                continue;
            
            gameObjectPool = pool;
            break;
        }

        if (gameObjectPool == null)
        {
            Debug.Log($"Object '{gameObject.name}' wasn't spawned from a pool. Destroying it instead.");
            Object.Destroy(gameObject);
        }
        else
        {
            gameObjectPool.Despawn(gameObject);
        }
    }

    #endregion

    public static int GetStackCount(GameObject prefab)
    {
        if (!prefab)
            return 0;

        return Pools.ContainsKey(prefab.GetInstanceID()) ? Pools[prefab.GetInstanceID()].StackCount : 0;
    }
    
    public static void ClearPool()
    {
        if (Pools is not { Count: > 0 }) 
            return;
        
        foreach (var pool in Pools.Values)
            pool.Dispose();

        Pools.Clear();
    }
}