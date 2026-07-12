using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Canvases
{
    public interface IUICanvas
    {
        public CanvasCategory CanvasCategory { get; }
        public Transform CanvasTransform { get; }
    }
}
