using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;
using DracoRuan.CoreSystems.PlayerLoopSystem.UpdateServices;
using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class AutoDespawn : MonoBehaviour, IUpdateHandler
    {
        [SerializeField] private float duration = 1f;

        private bool _isExpired;
        private float _timeCounter;
        
        private void OnEnable()
        {
            this._timeCounter = 0f;
            this._isExpired = false;
            UpdateServiceManager.RegisterUpdateHandler(this);
        }

        public void Tick(float deltaTime)
        {
            if (this._isExpired)
                return;
            
            this._timeCounter += deltaTime;
            if (this._timeCounter < this.duration) 
                return;
            
            this._isExpired = true;
            ObjectPooling.Despawn(this);
        }

        private void OnDisable()
        {
            UpdateServiceManager.DeregisterUpdateHandler(this);
        }
    }
}