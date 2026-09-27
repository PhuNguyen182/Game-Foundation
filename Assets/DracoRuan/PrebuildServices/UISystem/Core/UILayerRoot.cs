using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>Runtime state for one built layer (its root Canvas, sort allocator, shared backdrop).</summary>
    internal sealed class UILayerRoot
    {
        public UILayerDefinition Definition;
        public Canvas Canvas;
        public RectTransform RectTransform;
        public SortOrderAllocator SortOrderAllocator;
        public GameObject BackdropGameObject;
        public Canvas BackdropCanvas;
        public Button BackdropButton;

        /// <summary>
        /// Where view instances actually parent (UIService.AcquireInstanceAsync). Equal to
        /// RectTransform when the layer has no safe-area fitting; otherwise a child rect
        /// carrying a SafeAreaFitter, so views land inside the safe area while the backdrop
        /// (parented to RectTransform itself) still covers the full screen.
        /// </summary>
        public RectTransform ContentRectTransform;
    }
}
