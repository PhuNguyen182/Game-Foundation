using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Data
{
    public enum UIRenderMode
    {
        ScreenSpaceOverlay,
        ScreenSpaceCamera,
    }

    /// <summary>
    /// Describes how the layer roots are built at runtime: which layers exist, the
    /// CanvasScaler setup they share, and the render mode (Overlay, or Screen Space -
    /// Camera for URP camera stacking with particles/3D models in UI).
    /// </summary>
    [CreateAssetMenu(fileName = "UIRootConfig", menuName = "DracoRuan/UISystem/Root Config")]
    public sealed class UIRootConfig : ScriptableObject
    {
        [SerializeField] private List<UILayerDefinition> layers = new List<UILayerDefinition>();
        [SerializeField] private UIRenderMode renderMode = UIRenderMode.ScreenSpaceOverlay;
        [SerializeField] private Camera uiCamera;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;
        [SerializeField] private float planeDistance = 100f;
        [SerializeField] private bool pixelPerfect;
        [SerializeField] private float minUIScale = 0.75f;
        [SerializeField] private float maxUIScale = 1.5f;

        public IReadOnlyList<UILayerDefinition> Layers => this.layers;
        public UIRenderMode RenderMode => this.renderMode;
        public Camera UICamera => this.uiCamera;
        public Vector2 ReferenceResolution => this.referenceResolution;
        public float MatchWidthOrHeight => this.matchWidthOrHeight;
        public float PlaneDistance => this.planeDistance;
        public bool PixelPerfect => this.pixelPerfect;
        public float MinUIScale => this.minUIScale;
        public float MaxUIScale => this.maxUIScale;
    }
}
