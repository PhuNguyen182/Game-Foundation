using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Canvases
{
    public interface IUICanvas
    {
        public CanvasCategory CanvasCategory { get; }
        public Transform CanvasTransform { get; }
    }
}
