using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Canvases
{
    public class UICanvasManager : MonoBehaviour, IUICanvasManager
    {
        [SerializeField] private UICanvas[] canvases;
        
        public IUICanvas GetCanvas(CanvasCategory canvasCategory)
        {
            int count = this.canvases.Length;
            for (int i = 0; i < count; i++)
            {
                if (this.canvases[i].CanvasCategory == canvasCategory)
                    return this.canvases[i];
            }
            
            return null;
        }
    }
}
