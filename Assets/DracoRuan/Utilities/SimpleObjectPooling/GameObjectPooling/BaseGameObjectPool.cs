using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseGameObjectPool : IDisposable
{
    public abstract int StackCount { get; }
    protected abstract HashSet<int> MemberIDs { get; }

    public bool ContainsInstance(int instanceId) => this.MemberIDs.Contains(instanceId);
    
    public abstract void Preload(int initialQuantity, Transform parent = null);
    public abstract void Despawn(GameObject gameObject);
    public abstract void Dispose();
}
