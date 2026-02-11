using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class SimpleGameObjectPool : BaseGameObjectPool
{
    private int _nextId;
    private bool _disposed;
    
    private Queue<GameObject> InactiveGameObjects { get; }
    private readonly GameObject _prefab;

    protected override HashSet<int> MemberIDs { get; }
    public override int StackCount => InactiveGameObjects.Count;
    
    public SimpleGameObjectPool(GameObject prefab, int initialQuantity)
    {
        this._nextId = 1;
        this._prefab = prefab;
        this.InactiveGameObjects = new Queue<GameObject>(initialQuantity);
        this.MemberIDs = new HashSet<int>();
    }

    public override void Preload(int initialQuantity, Transform parent = null)
    {
        for (int i = 0; i < initialQuantity; i++)
        {
            GameObject gameObject = Object.Instantiate(this._prefab, parent);
            gameObject.name = $"{this._prefab.name} ({this._nextId++})";
            this.MemberIDs.Add(gameObject.GetInstanceID());
            gameObject.SetActive(false);
            this.InactiveGameObjects.Enqueue(gameObject);
        }
    }

    public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStay)
    {
        while (true)
        {
            GameObject gameObject;
            if (this.InactiveGameObjects.Count == 0)
            {
                gameObject = Object.Instantiate(this._prefab, position, rotation, parent);
                gameObject.name = $"{this._prefab.name} ({this._nextId++})";
                MemberIDs.Add(gameObject.GetInstanceID());
            }
            else
            {
                gameObject = this.InactiveGameObjects.Dequeue();
                if (!gameObject)
                    continue;
            }

            gameObject.transform.SetPositionAndRotation(position, rotation);
            if (parent)
                gameObject.transform.SetParent(parent, worldPositionStay);

            gameObject.SetActive(true);
            return gameObject;
        }
    }
    
    public override void Despawn(GameObject gameObject)
    {
        if (!gameObject.activeSelf)
            return;

        gameObject.SetActive(false);
        this.InactiveGameObjects.Enqueue(gameObject);
    }
    
    public override void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (this._disposed)
            return;

        if (disposing)
        {
            this.InactiveGameObjects.Clear();
            this.MemberIDs.Clear();
        }

        this._disposed = true;
    }
    
    ~SimpleGameObjectPool()
    {
        this.Dispose(false);
    }
}