using System;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data;
using UnityEngine;
using VContainer;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// Registers a scene/additive-scope's own registry + resolver with the root UIService, so
    /// view models declared in that scope's registry are created from that scope's resolver
    /// (and can inject that scope's services). Also carries the scope's optional base camera
    /// and scaler overrides, both undone on dispose. Disposing the scope (its owning
    /// LifetimeScope disposing) force-closes any views it opened, with no transition.
    /// </summary>
    public sealed class UIScope : IDisposable
    {
        internal readonly UIRegistry Registry;
        internal readonly IObjectResolver Resolver;
        internal readonly UIScopeOverrides Overrides;
        internal readonly Camera BaseCamera;

        private readonly UIService _owner;
        private bool _isDisposed;

        internal UIScope(UIService owner, UIRegistry registry, IObjectResolver resolver,
            UIScopeOverrides overrides = null, Camera baseCamera = null)
        {
            this._owner = owner;
            this.Registry = registry;
            this.Resolver = resolver;
            this.Overrides = overrides;
            this.BaseCamera = baseCamera;

            if (baseCamera)
                UIBaseCameras.Register(baseCamera);
        }

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;

            if (this.BaseCamera)
                UIBaseCameras.Unregister(this.BaseCamera);

            this._owner.UnregisterScope(this);
        }
    }
}
