using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// Pipeline seam for the always-alive UI camera (CAMERA_INPUT_PLAN.md, Quyết định 2). The core
    /// only knows this interface; the URP implementation lives in the UISystem.URP adapter and a
    /// plain Built-in one ships in the core (BuiltInCameraStacker). baseCamera may be null in
    /// Detach when the previous base camera was already destroyed.
    /// </summary>
    public interface IUICameraStacker
    {
        /// <summary>True when this stacker can drive the currently active render pipeline.</summary>
        bool IsSupported { get; }

        /// <summary>One-time setup of a freshly created UI camera (strip post-processing etc.).</summary>
        void Configure(Camera uiCamera);

        /// <summary>Make uiCamera render on top of baseCamera.</summary>
        void Attach(Camera uiCamera, Camera baseCamera);

        /// <summary>Stop rendering on top of baseCamera; uiCamera then renders on its own.</summary>
        void Detach(Camera uiCamera, Camera baseCamera);
    }
}
