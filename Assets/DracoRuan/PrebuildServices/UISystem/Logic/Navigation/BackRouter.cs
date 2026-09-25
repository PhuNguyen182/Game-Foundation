using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.UISystem.Logic
{
    /// <summary>
    /// Routes a Back/Esc request through UI layers from highest to lowest
    /// (e.g. System &gt; Popup &gt; Screen). Layers are registered in that order; each
    /// is asked in turn until one reports Close/Consume. If every layer passes
    /// through, BackAtRoot fires so the game can decide what "back" means globally
    /// (e.g. prompt to quit).
    /// </summary>
    public sealed class BackRouter
    {
        private readonly List<Func<UIBackResult>> _layers = new List<Func<UIBackResult>>();

        public event Action BackAtRoot;

        public void RegisterLayer(Func<UIBackResult> handler) => this._layers.Add(handler);

        /// <summary>Returns true if some layer handled the Back request.</summary>
        public bool TryHandleBack(bool isLocked)
        {
            if (isLocked)
                return false;

            foreach (Func<UIBackResult> layer in this._layers)
            {
                UIBackResult result = layer();
                if (result != UIBackResult.PassThrough)
                    return true;
            }

            this.BackAtRoot?.Invoke();
            return false;
        }
    }
}
