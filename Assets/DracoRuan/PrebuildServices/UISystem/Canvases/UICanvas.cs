using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Canvases
{
    [RequireComponent(typeof(Canvas))]
    public class UICanvas : MonoBehaviour, IUICanvas
    {
        [SerializeField] private CanvasCategory canvasCategory;
        [SerializeField] private Canvas canvas;

        public CanvasCategory CanvasCategory => this.canvasCategory;
        public Transform CanvasTransform => this.transform;

        private void ApplyCanvasConfig(CanvasConfig canvasConfig)
        {
            if (!canvasConfig || !this.canvas)
                return;

            this.canvas.renderMode = canvasConfig.renderMode;
            this.canvas.sortingOrder = canvasConfig.sortingOrder;
            this.canvas.pixelPerfect = canvasConfig.pixelPerfect;
            this.canvas.planeDistance = canvasConfig.planeDistance;
        }

#if UNITY_EDITOR
        private const string CanvasConfigCollectionPath = "CanvasConfigs/CanvasConfigCollection";
        
        private CanvasCategory _tempCategory;
        private CanvasConfigCollection _canvasConfigCollection;
        
        private void OnValidate()
        {
            if (!this.canvas)
                this.canvas = this.GetComponent<Canvas>();
            
            UpdateCanvasConfig();
            return;

            void UpdateCanvasConfig()
            {
                if (this.canvasCategory == this._tempCategory)
                    return;
                
                this._tempCategory = this.canvasCategory;
                if (!this._canvasConfigCollection)
                {
                    this._canvasConfigCollection = Resources.Load<CanvasConfigCollection>(CanvasConfigCollectionPath);
                    if (!_canvasConfigCollection)
                        return;
                }

                CanvasConfig canvasConfig = this._canvasConfigCollection.GetCanvasConfigByCategory(this.canvasCategory);
                this.ApplyCanvasConfig(canvasConfig);
            }
        }
#endif
    }
}
