using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Canvases
{
    [CreateAssetMenu(fileName = "CanvasConfig", menuName = "DracoRuan/UISystem/Canvases/CanvasConfig")]
    public class CanvasConfig : ScriptableObject
    {
        [SerializeField] public CanvasCategory canvasCategory;
        [SerializeField] public bool pixelPerfect;
        [SerializeField] public RenderMode renderMode = RenderMode.ScreenSpaceOverlay;
        [SerializeField] public float planeDistance = 100;
        [SerializeField] public int sortingOrder;
    }
}
