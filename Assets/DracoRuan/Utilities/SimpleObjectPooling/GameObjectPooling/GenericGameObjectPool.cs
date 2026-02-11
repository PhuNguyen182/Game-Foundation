using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class GenericGameObjectPool<T> : BaseGameObjectPool where T : Component
{
    private int _nextId;
    private bool _disposed;
    
    private readonly T _prefab;
    private Queue<T> InactiveInstances { get; }

    protected override HashSet<int> MemberIDs { get; }
    public override int StackCount => this.InactiveInstances.Count;
    
    public GenericGameObjectPool(T prefab, int initialQuantity)
    {
        this._nextId = 1;
        this._prefab = prefab;
        this.InactiveInstances = new Queue<T>(initialQuantity);
        this.MemberIDs = new HashSet<int>();
    }

    public override void Preload(int initialQuantity, Transform parent = null)
    {
        for (int i = 0; i < initialQuantity; i++)
        {
            T instance = Object.Instantiate(this._prefab, parent);
            instance.name = $"{this._prefab.name} ({this._nextId++})"; 
            this.MemberIDs.Add(instance.GetInstanceID());
            instance.gameObject.SetActive(false);
            this.InactiveInstances.Enqueue(instance);
        }
    }
    
    public T Spawn(Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStay)
    {
        while (true)
        {
            T instance;
            if (this.InactiveInstances.Count == 0)
            {
                instance = Object.Instantiate(this._prefab, position, rotation, parent);
                instance.name = $"{this._prefab.name} ({this._nextId++})";
                MemberIDs.Add(instance.GetInstanceID());
            }
            else
            {
                instance = this.InactiveInstances.Dequeue();
                if (!instance)
                    continue;
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            if (parent)
                instance.transform.SetParent(parent, worldPositionStay);

            instance.gameObject.SetActive(true);
            return instance;
        }
    }
    
    public override void Despawn(GameObject gameObject)
    {
        if (!gameObject.activeSelf)
            return;

        gameObject.SetActive(false);
        if (gameObject.TryGetComponent(out T component))
            this.InactiveInstances.Enqueue(component);
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
            this.InactiveInstances.Clear();
            this.MemberIDs.Clear();
        }

        this._disposed = true;
    }
    
    ~GenericGameObjectPool()
    {
        this.Dispose(false);
    }
}