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
    }
}
