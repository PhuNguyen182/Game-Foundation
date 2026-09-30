using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>
    /// One layer in the UI stack (e.g. Screen, Popup, System). Each layer becomes its own
    /// root Canvas at runtime; sortStep leaves room between consecutive views on the layer
    /// for their own sub-canvases/particles to sort within.
    /// </summary>
    [CreateAssetMenu(fileName = "UILayerDefinition", menuName = "DracoRuan/UISystem/Layer Definition")]
    public sealed class UILayerDefinition : ScriptableObject
    {
        [SerializeField] private string layerName = "Screen";
        [SerializeField] private UILayerKind kind = UILayerKind.Screen;
        [SerializeField] private int baseSortOrder;
        [SerializeField] private int sortStep = 10;
        [SerializeField] private bool applySafeArea;
        [SerializeField] private bool blocksInputBelow;

        public string LayerName => this.layerName;
        public UILayerKind Kind => this.kind;
        public int BaseSortOrder => this.baseSortOrder;
        public int SortStep => this.sortStep;
        public bool ApplySafeArea => this.applySafeArea;
        public bool BlocksInputBelow => this.blocksInputBelow;
    }
}
