using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;
using DracoRuan.CoreSystems.PlayerLoopSystem.UpdateServices;
using UnityEngine;

/// <summary>
/// This component is used to return to the object pool if it is spawned from an object pool.
/// Attach this component in to the game object need to be despawned automatically.
/// </summary>
public class AutoDespawn : MonoBehaviour, IUpdateHandler
{
    [SerializeField] private float duration = 1;

    private float _timer;

    private void OnEnable()
    {
        this._timer = 0;
        UpdateServiceManager.RegisterUpdateHandler(this);
    }

    public void Tick(float deltaTime)
    {
        this._timer += deltaTime;

        if (!(this._timer >= duration)) 
            return;
        
        this._timer = 0;
        GameObjectPoolManager.Despawn(this.gameObject);
    }

    public void SetDuration(float despawnDuration) => this.duration = despawnDuration;

    private void OnDisable() => UpdateServiceManager.DeregisterUpdateHandler(this);
}
