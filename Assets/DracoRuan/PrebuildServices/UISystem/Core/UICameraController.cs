using System;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// Keeps the always-alive UI camera attached to whichever base camera is current
    /// (UIBaseCameras), and switches it to a standalone solid-black Base camera while there is
    /// none (scene-transition gap), so a loading screen still renders.
    /// </summary>
    public sealed class UICameraController : IDisposable
    {
        private readonly IUICameraStacker _stacker;
        private Camera _attachedBase;
        private Camera _fallback;
        private bool _standalone;

        public Camera Camera { get; }

        /// <summary>Base camera the UI camera currently sits on top of, null when standalone.</summary>
        public Camera AttachedBase => this._attachedBase ? this._attachedBase : null;

        public bool IsStandalone => this._standalone;

        public UICameraController(Camera uiCamera, IUICameraStacker stacker)
        {
            this.Camera = uiCamera;
            this._stacker = stacker;
            this._stacker.Configure(uiCamera);
            UIBaseCameras.Changed += this.Refresh;
            this.Refresh();
        }

        /// <summary>Call once per scene change: with no explicitly registered camera, adopts
        /// Camera.main. Never polled per frame.</summary>
        public void ResolveFallback()
        {
            if (!UIBaseCameras.Current)
                this._fallback = Camera.main;

            this.Refresh();
        }

        public void Refresh()
        {
            Camera target = UIBaseCameras.Current;
            if (!target && this._fallback)
                target = this._fallback;

            if (target)
            {
                if (ReferenceEquals(target, this._attachedBase))
                    return;

                if (this._attachedBase || !this._standalone)
                    this._stacker.Detach(this.Camera, this._attachedBase);

                this._stacker.Attach(this.Camera, target);
                this._attachedBase = target;
                this._standalone = false;
                return;
            }

            if (this._standalone)
                return;

            this._stacker.Detach(this.Camera, this._attachedBase);
            this.Camera.clearFlags = CameraClearFlags.SolidColor;
            this.Camera.backgroundColor = Color.black;
            this._attachedBase = null;
            this._standalone = true;
        }

        public void Dispose() => UIBaseCameras.Changed -= this.Refresh;
    }
}
