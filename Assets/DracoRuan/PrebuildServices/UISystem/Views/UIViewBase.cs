using System;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion;
using UnityEngine;
using UnityEngine.UI;
using UIMotion = DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion.UIMotion;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views
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
    public abstract class UIViewBase : MonoBehaviour, IUIMotionTriggerSource
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private UIMotion motion;
        
        private bool _motionResolved;

        public Canvas Canvas => this.canvas ? this.canvas : this.canvas = this.GetComponent<Canvas>();

        public GraphicRaycaster GraphicRaycaster => this.graphicRaycaster
            ? this.graphicRaycaster
            : this.graphicRaycaster = this.GetComponent<GraphicRaycaster>();

        public CanvasGroup CanvasGroup => this.canvasGroup
            ? this.canvasGroup
            : this.canvasGroup = this.GetComponent<CanvasGroup>();

        /// <summary>Optional: a view with no UIMotion on its root opens/closes Instant (see
        /// REWRITE_PLAN.md scenario (p)). Resolved once and cached, same pattern as Canvas
        /// above, except the cached result itself may legitimately be null (no UIMotion on
        /// this prefab), so a separate `_motionResolved` flag - not a `!= null` check - guards
        /// re-resolution.</summary>
        public UIMotion Motion
        {
            get
            {
                if (this._motionResolved) 
                    return this.motion;

                if (!this.motion)
                    this.motion = this.GetComponent<UIMotion>();
                
                this._motionResolved = true;
                return this.motion;
            }
        }

        /// <summary>Raised right after this view becomes visible again (Show completes),
        /// so child UIMotion components using the OnParentShow trigger can replay - see
        /// IUIMotionTriggerSource. UIView hides via Canvas.enabled, which does not re-run a
        /// child's OnEnable.</summary>
        public event Action ParentShown;

        /// <summary>Called by the router right after the Show transition finishes. Not
        /// virtual: firing ParentShown is framework wiring, not a view hook (OnOpened is
        /// the hook for that).</summary>
        internal void RaiseParentShown() => this.ParentShown?.Invoke();

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

        /// <summary>The Selectable UIFocusController.Restore should fall back to for this view
        /// when no remembered selection is valid and the last input device was keyboard/gamepad.
        /// Non-generic so UIService (which only ever holds a UIViewBase reference) can read it
        /// without knowing TVM; UIView&lt;TVM&gt; is the only place a concrete value is set.</summary>
        public virtual Selectable DefaultSelectable => null;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.canvas)
                this.canvas = this.GetComponent<Canvas>();
            
            if (!this.canvasGroup)
                this.canvasGroup = this.GetComponent<CanvasGroup>();
            
            if (!this.graphicRaycaster)
                this.graphicRaycaster = this.GetComponent<GraphicRaycaster>();
        }
#endif
    }
}
