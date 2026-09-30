using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data
{
    public enum UIRenderMode
    {
        ScreenSpaceOverlay,
        ScreenSpaceCamera,
    }

    /// <summary>
    /// Describes how the layer roots are built at runtime: which layers exist, the
    /// CanvasScaler setup they share, and the render mode (Overlay, or Screen Space -
    /// Camera for camera stacking with particles/3D models in UI).
    /// </summary>
    [CreateAssetMenu(fileName = "UIRootConfig", menuName = "DracoRuan/UISystem/Root Config")]
    public sealed class UIRootConfig : ScriptableObject
    {
        [SerializeField] private List<UILayerDefinition> layers = new();
        [SerializeField] private UIRenderMode renderMode = UIRenderMode.ScreenSpaceOverlay;
        [Tooltip("Screen Space - Camera only. Optional prefab for the always-alive UI camera; when empty a default orthographic camera (UI layer only, no post-processing) is created.")]
        [SerializeField] private Camera uiCameraPrefab;
        [SerializeField] private Vector2 referenceResolution = new(1080f, 1920f);
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;
        [SerializeField] private float planeDistance = 100f;
        [SerializeField] private bool pixelPerfect;
        [Tooltip("UISystem owns one EventSystem that survives scene loads and disables any other EventSystem found in a loaded scene.")]
        [SerializeField] private bool manageEventSystem = true;

        public IReadOnlyList<UILayerDefinition> Layers => this.layers;
        public UIRenderMode RenderMode => this.renderMode;
        public Camera UICameraPrefab => this.uiCameraPrefab;
        public Vector2 ReferenceResolution => this.referenceResolution;
        public float MatchWidthOrHeight => this.matchWidthOrHeight;
        public float PlaneDistance => this.planeDistance;
        public bool PixelPerfect => this.pixelPerfect;
        public bool ManageEventSystem => this.manageEventSystem;
    }
}
