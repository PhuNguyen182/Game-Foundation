using System;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;
using UnityEngine;

namespace DracoRuan.Utilities.ObjectPooling
{
    public class AutoDespawnByTimer : MonoBehaviour, IUpdateHandler
    {
        [SerializeField] private float duration = 1f;

        private bool _isExpired;
        private float _timeCounter;
        
        public event Action OnDespawned; 
        
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
            this.OnDespawned?.Invoke();
        }

        private void OnDisable()
        {
            UpdateServiceManager.DeregisterUpdateHandler(this);
        }
    }
}