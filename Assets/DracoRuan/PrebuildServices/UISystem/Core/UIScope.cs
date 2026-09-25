using System;
using VContainer;

namespace DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// Registers a scene/additive-scope's own registry + resolver with the root UIService, so
    /// view models declared in that scope's registry are created from that scope's resolver
    /// (and can inject that scope's services). Disposing the scope (its owning LifetimeScope
    /// disposing) force-closes any views it opened, with no transition.
    /// </summary>
    public sealed class UIScope : IDisposable
    {
        internal readonly UIRegistry Registry;
        internal readonly IObjectResolver Resolver;

        private readonly UIService _owner;
        private bool _isDisposed;

        internal UIScope(UIService owner, UIRegistry registry, IObjectResolver resolver)
        {
            this._owner = owner;
            this.Registry = registry;
            this.Resolver = resolver;
        }

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;
            this._owner.UnregisterScope(this);
        }
    }
}
