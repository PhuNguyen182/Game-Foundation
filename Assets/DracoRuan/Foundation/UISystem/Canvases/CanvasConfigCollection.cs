using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Canvases
{
    [CreateAssetMenu(fileName = "CanvasConfigCollection", menuName = "DracoRuan/UISystem/Canvases/CanvasConfigCollection")]
    public class CanvasConfigCollection : ScriptableObject
    {
        [SerializeField] public CanvasConfig[] canvasConfigs;

        public CanvasConfig GetCanvasConfigByCategory(CanvasCategory canvasCategory)
        {
            int count = this.canvasConfigs.Length;
            for (int i = 0; i < count; i++)
            {
                if (this.canvasConfigs[i].canvasCategory == canvasCategory)
                    return this.canvasConfigs[i];
            }
            
            return null;
        }
    }
}
