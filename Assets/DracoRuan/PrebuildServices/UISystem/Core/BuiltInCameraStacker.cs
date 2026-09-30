using UnityEngine;
using UnityEngine.Rendering;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>Built-in render pipeline stacking: a higher-depth camera that only clears depth.</summary>
    public sealed class BuiltInCameraStacker : IUICameraStacker
    {
        public bool IsSupported => !GraphicsSettings.currentRenderPipeline;

        public void Configure(Camera uiCamera)
        {
            uiCamera.allowHDR = false;
            uiCamera.allowMSAA = false;
            uiCamera.useOcclusionCulling = false;
        }

        public void Attach(Camera uiCamera, Camera baseCamera)
        {
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.depth = baseCamera.depth + 1f;
        }

        public void Detach(Camera uiCamera, Camera baseCamera)
        {
        }
    }
}
