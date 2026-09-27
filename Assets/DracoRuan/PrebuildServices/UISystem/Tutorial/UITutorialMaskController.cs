using DracoRuan.PrebuildServices.UISystem.Motion;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Tutorial
{
    /// <summary>
    /// Drives one shared TutorialMask material (see UITutorialMask.shader): moves/resizes the
    /// visible cutout to match a highlighted RectTransform, and keeps 4 invisible raycast
    /// blockers positioned around that same rect so clicks land on the highlighted UI
    /// underneath while everywhere else stays blocked (REWRITE_PLAN.md mục 5 bước 10: "tutorial
    /// highlight hook, mask lỗ + chặn input ngoài vùng"). The shader hole is purely visual -
    /// Unity's raycasting is RectTransform geometry, not shader/alpha, so it can't punch a
    /// raycast hole by itself; the 4 blockers are what actually gate input (see the ruling in
    /// PROGRESS.md/this class's own comments for why 4 rects instead of a per-pixel alpha
    /// raycast filter - simpler, and exact sprite-shape click-through wasn't a requirement).
    /// Implements IUIMotionCustomTrack so a UIMotion timeline can animate the move between two
    /// highlight targets using the existing ease/duration/schedule machinery instead of a
    /// bespoke tween - see UITutorialStep.moveTrack.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UITutorialMaskController : MonoBehaviour, IUIMotionCustomTrack
    {
        [SerializeField] private RectTransform screenRect;
        [SerializeField] private Image maskImage;
        [SerializeField] private Sprite defaultMaskSprite;

        [Header("Raycast blockers (Top/Bottom/Left/Right), auto-created if left empty")]
        [SerializeField] private RectTransform blockerTop;
        [SerializeField] private RectTransform blockerBottom;
        [SerializeField] private RectTransform blockerLeft;
        [SerializeField] private RectTransform blockerRight;

        private Material _materialInstance;
        private RectTransform _toTarget;
        private Vector4 _fromRect;
        private Vector4 _toRect;

        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int MaskRectId = Shader.PropertyToID("_MaskRect");

        private RectTransform RectTransform => this.screenRect != null ? this.screenRect : this.screenRect = (RectTransform)this.transform;

        private void Awake()
        {
            if (this.maskImage == null)
                this.maskImage = this.GetComponent<Image>();

            Shader shader = this.maskImage.material != null && this.maskImage.material.shader.name == "DracoRuan/UISystem/TutorialMask"
                ? this.maskImage.material.shader
                : Shader.Find("DracoRuan/UISystem/TutorialMask");
            this._materialInstance = new Material(shader);
            this.maskImage.material = this._materialInstance;
            this.maskImage.raycastTarget = false; // the 4 blockers below do the actual gating

            this.EnsureBlocker(ref this.blockerTop, "BlockerTop");
            this.EnsureBlocker(ref this.blockerBottom, "BlockerBottom");
            this.EnsureBlocker(ref this.blockerLeft, "BlockerLeft");
            this.EnsureBlocker(ref this.blockerRight, "BlockerRight");
        }

        private void EnsureBlocker(ref RectTransform blocker, string name)
        {
            if (blocker != null)
                return;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(this.RectTransform, false);
            var image = go.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            blocker = (RectTransform)go.transform;
        }

        private void OnDestroy()
        {
            if (this._materialInstance != null)
                Destroy(this._materialInstance);
        }

        /// <summary>Immediately snaps the cutout and its raycast blockers onto `target` using
        /// `sprite` (or the controller's default sprite if null) as the hole's visual shape -
        /// no animation, for the first step or a scripted jump.</summary>
        public void SnapTo(RectTransform target, Sprite sprite = null)
        {
            this.ApplySprite(sprite);
            Vector4 rect = ComputeMaskRect(target, this.RectTransform);
            this._materialInstance.SetVector(MaskRectId, rect);
            this.ApplyBlockers(rect);
        }

        /// <summary>Sets up a move for the next UIMotion Custom-track playback (CaptureStart/
        /// Sample/Snap below) from wherever the mask currently is to `target`.</summary>
        public void PrepareMoveTo(RectTransform target, Sprite sprite = null)
        {
            this.ApplySprite(sprite);
            this._toTarget = target;
        }

        private void ApplySprite(Sprite sprite)
        {
            Sprite resolved = sprite != null ? sprite : this.defaultMaskSprite;
            if (resolved != null)
                this._materialInstance.SetTexture(MaskTexId, resolved.texture);
        }

        void IUIMotionCustomTrack.CaptureStart()
        {
            Vector4 current = (Vector4)this._materialInstance.GetVector(MaskRectId);
            this._fromRect = current;
            this._toRect = this._toTarget != null ? ComputeMaskRect(this._toTarget, this.RectTransform) : current;
        }

        void IUIMotionCustomTrack.Sample(float t)
        {
            Vector4 lerped = Vector4.LerpUnclamped(this._fromRect, this._toRect, t);
            this._materialInstance.SetVector(MaskRectId, lerped);
            this.ApplyBlockers(lerped);
        }

        void IUIMotionCustomTrack.Snap(bool toEnd)
        {
            Vector4 rect = toEnd ? this._toRect : this._fromRect;
            this._materialInstance.SetVector(MaskRectId, rect);
            this.ApplyBlockers(rect);
        }

        /// <summary>Positions the 4 raycast blockers so together they cover the full screen
        /// minus `normalizedRect` (center.xy, halfSize.zw, normalized 0-1) - top/bottom span the
        /// full width, left/right fill exactly the band between them at the hole's own height,
        /// so the four never overlap and never leave a gap.</summary>
        private void ApplyBlockers(Vector4 normalizedRect)
        {
            (Vector4 top, Vector4 bottom, Vector4 left, Vector4 right) = ComputeBlockerRects(normalizedRect);

            SetStretch(this.blockerTop, top.x, top.y, top.z, top.w);
            SetStretch(this.blockerBottom, bottom.x, bottom.y, bottom.z, bottom.w);
            SetStretch(this.blockerLeft, left.x, left.y, left.z, left.w);
            SetStretch(this.blockerRight, right.x, right.y, right.z, right.w);
        }

        /// <summary>Pure math (no RectTransform dependency, testable in isolation): each result
        /// is (xMin, yMin, xMax, yMax) in normalized screen space for one of the 4 blockers, that
        /// together cover the full screen minus `normalizedRect` (center.xy, halfSize.zw) without
        /// overlapping or leaving a gap - top/bottom span the full width, left/right fill exactly
        /// the band between them at the hole's own height.</summary>
        internal static (Vector4 top, Vector4 bottom, Vector4 left, Vector4 right) ComputeBlockerRects(Vector4 normalizedRect)
        {
            float left = normalizedRect.x - normalizedRect.z;
            float right = normalizedRect.x + normalizedRect.z;
            float bottom = normalizedRect.y - normalizedRect.w;
            float top = normalizedRect.y + normalizedRect.w;

            return (
                new Vector4(0f, top, 1f, 1f),
                new Vector4(0f, 0f, 1f, bottom),
                new Vector4(0f, bottom, left, top),
                new Vector4(right, bottom, 1f, top));
        }

        private static void SetStretch(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            xMin = Mathf.Clamp01(xMin);
            xMax = Mathf.Clamp01(xMax);
            yMin = Mathf.Clamp01(yMin);
            yMax = Mathf.Clamp01(yMax);

            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Screen-space (center.xy, halfSize.zw), normalized 0-1 against `screenRect` -
        /// matches _MaskRect's contract in UITutorialMask.shader.</summary>
        private static Vector4 ComputeMaskRect(RectTransform target, RectTransform screenRect)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            var screenCorners = new Vector3[4];
            screenRect.GetWorldCorners(screenCorners);

            Vector3 screenMin = screenCorners[0];
            Vector3 screenSize = screenCorners[2] - screenCorners[0];

            Vector3 targetMin = corners[0];
            Vector3 targetMax = corners[2];

            Vector2 normMin = new Vector2(
                (targetMin.x - screenMin.x) / screenSize.x,
                (targetMin.y - screenMin.y) / screenSize.y);
            Vector2 normMax = new Vector2(
                (targetMax.x - screenMin.x) / screenSize.x,
                (targetMax.y - screenMin.y) / screenSize.y);

            Vector2 center = (normMin + normMax) * 0.5f;
            Vector2 halfSize = (normMax - normMin) * 0.5f;

            return new Vector4(center.x, center.y, halfSize.x, halfSize.y);
        }
    }
}
