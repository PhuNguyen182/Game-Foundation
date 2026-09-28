using System;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// Fits its RectTransform to Screen.safeArea (REWRITE_PLAN.md 2.5: layer root gets one
    /// when the layer's UILayerDefinition.ApplySafeArea is set). Re-applies whenever the
    /// safe area actually changes (rotation, notch-aware resolution change) rather than every
    /// frame, to avoid needless layout rebuilds - checked cheaply once per Update via a cached
    /// Rect comparison.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        
        private ScreenOrientation _lastOrientation;
        private Rect _lastSafeArea;

        private RectTransform RectTransform => this.rectTransform
            ? this.rectTransform
            : this.rectTransform = (RectTransform)this.transform;

        private void OnEnable() => this.Apply(force: true);

        private void Update() => this.Apply(force: false);

        private void Apply(bool force)
        {
            Rect safeArea = Screen.safeArea;
            if (!force && safeArea == this._lastSafeArea && Screen.orientation == this._lastOrientation)
                return;

            this._lastSafeArea = safeArea;
            this._lastOrientation = Screen.orientation;

            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            RectTransform rt = this.RectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Test-only hook (see AssemblyInfo.cs InternalsVisibleTo): runs Apply
        /// synchronously without depending on OnEnable/Update timing.</summary>
        internal void ApplyForTest() => this.Apply(force: true);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.rectTransform)
                this.rectTransform = (RectTransform)this.transform;
        }
#endif
    }
}
