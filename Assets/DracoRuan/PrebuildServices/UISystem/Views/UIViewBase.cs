using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Views
{
    /// <summary>
    /// Root component of every routed view prefab. Deliberately does not use Unity message
    /// virtuals (Awake/OnDestroy) for framework logic: a subclass re-declaring one of those as
    /// `new virtual` silently hides the base implementation (the exact bug class the old
    /// UISystem had — base cleanup never ran). The framework calls the hooks below directly
    /// instead. Hooks are for visuals/VFX only; business logic belongs in the view model.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(GraphicRaycaster))]
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIViewBase : MonoBehaviour
    {
        private Canvas _canvas;
        private GraphicRaycaster _graphicRaycaster;
        private CanvasGroup _canvasGroup;

        public Canvas Canvas => this._canvas != null ? this._canvas : this._canvas = this.GetComponent<Canvas>();

        public GraphicRaycaster GraphicRaycaster => this._graphicRaycaster != null
            ? this._graphicRaycaster
            : this._graphicRaycaster = this.GetComponent<GraphicRaycaster>();

        public CanvasGroup CanvasGroup => this._canvasGroup != null
            ? this._canvasGroup
            : this._canvasGroup = this.GetComponent<CanvasGroup>();

        /// <summary>Called once, right after Instantiate, before the first Show.</summary>
        protected internal virtual void OnCreated()
        {
        }

        /// <summary>Called when the Show transition starts (input is locked at this point).</summary>
        protected internal virtual void OnOpening()
        {
        }

        /// <summary>Called when the Show transition finishes and the view is interactable.</summary>
        protected internal virtual void OnOpened()
        {
        }

        /// <summary>Called when the Hide transition starts.</summary>
        protected internal virtual void OnClosing()
        {
        }

        /// <summary>Called when the Hide transition finishes, right before the view is hidden/released.</summary>
        protected internal virtual void OnClosed()
        {
        }

        /// <summary>Called when another view above this one closes and this one becomes topmost again.</summary>
        protected internal virtual void OnFocused()
        {
        }

        /// <summary>Called when a view opens above this one and this one is no longer topmost.</summary>
        protected internal virtual void OnBlurred()
        {
        }
    }
}
