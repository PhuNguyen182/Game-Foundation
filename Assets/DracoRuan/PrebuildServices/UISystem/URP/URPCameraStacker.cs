using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DracoRuan.PrebuildServices.UISystem.URP.DracoRuan.PrebuildServices.UISystem.URP
{
    /// <summary>
    /// URP camera stacking: the UI camera is an Overlay camera added to the current base camera's
    /// cameraStack, and turns into a standalone Base camera when no base camera exists.
    /// Registered automatically at startup; only reports support when URP is the active pipeline.
    /// </summary>
    public sealed class URPCameraStacker : IUICameraStacker
    {
        public bool IsSupported => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

        public void Configure(Camera uiCamera)
        {
            UniversalAdditionalCameraData data = uiCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            data.antialiasing = AntialiasingMode.None;
            data.volumeLayerMask = 0;
        }

        public void Attach(Camera uiCamera, Camera baseCamera)
        {
            uiCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;

            var stack = baseCamera.GetUniversalAdditionalCameraData().cameraStack;
            if (!stack.Contains(uiCamera))
                stack.Add(uiCamera);
        }

        public void Detach(Camera uiCamera, Camera baseCamera)
        {
            if (baseCamera)
                baseCamera.GetUniversalAdditionalCameraData().cameraStack.Remove(uiCamera);

            uiCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterWithCore() => UICameraStackerProvider.Register(new URPCameraStacker());
    }
}
