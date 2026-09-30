using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// Put on a scene's gameplay camera: while enabled, the UI camera renders on top of it
    /// (Screen Space - Camera mode). Registrations are a stack, so a cutscene camera can
    /// override the gameplay camera and hand the UI back when disabled.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class UIBaseCameraBinder : MonoBehaviour
    {
        [SerializeField] private Camera uiCamera;

        private void OnEnable()
        {
            if (!this.uiCamera)
                this.uiCamera = this.GetComponent<Camera>();

            UIBaseCameras.Register(this.uiCamera);
        }

        private void OnDisable() => UIBaseCameras.Unregister(this.uiCamera);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.uiCamera)
                this.uiCamera = this.GetComponent<Camera>();
        }
#endif
    }
}
