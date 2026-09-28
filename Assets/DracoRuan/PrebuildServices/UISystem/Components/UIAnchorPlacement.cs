using System;
using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// Anchors this RectTransform next to a target RectTransform - REWRITE_PLAN.md 2.5
    /// (Hint/Tooltip): picks a side from `sidePriority` by available screen space, clamps
    /// inside the safe area, converts between Overlay and Camera canvases via world space (the
    /// only coordinate frame both render modes agree on), and optionally re-places every
    /// LateUpdate to follow a moving/scrolling target.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIAnchorPlacement : MonoBehaviour
    {
        [SerializeField] private RectTransform target;

        [SerializeField] private UIAnchorSide[] sidePriority =
            { UIAnchorSide.Above, UIAnchorSide.Below, UIAnchorSide.Right, UIAnchorSide.Left };

        [SerializeField] private float spacing = 8f;
        [SerializeField] private bool followTarget;
        [SerializeField] private RectTransform rectTransform;

        private Canvas _canvas;

        public RectTransform Target
        {
            get => this.target;
            set => this.target = value;
        }

        private RectTransform RectTransform => this.rectTransform
            ? this.rectTransform
            : this.rectTransform = (RectTransform)this.transform;

        private Canvas Canvas => this._canvas ? this._canvas : this._canvas = this.GetComponentInParent<Canvas>();

        private void OnEnable() => this.Reposition();

        private void LateUpdate()
        {
            if (this.followTarget)
                this.Reposition();
        }

        /// <summary>Recomputes side + position against the current target. No-op if Target is
        /// unset (nothing to anchor against yet).</summary>
        public void Reposition()
        {
            if (!this.target)
                return;

            RectTransform self = this.RectTransform;
            Canvas myCanvas = this.Canvas;
            Camera myCamera = CanvasCamera(myCanvas);
            Camera targetCamera = CanvasCamera(this.target.GetComponentInParent<Canvas>());

            // self.rect.size is in this RectTransform's own local units; comparing it against
            // target's WORLD-corner-derived screen rect is only exact when both canvases share
            // the same scale factor (true for every layer this project builds - all under one
            // UIRootConfig/CanvasScaler set). A mixed-scale setup would need selfSize itself
            // converted through world corners the same way targetScreenRect is below.
            Vector2 selfSize = self.rect.size;

            Rect targetScreenRect = WorldCornersToScreenRect(this.target, targetCamera);
            Rect safeAreaScreenRect = Screen.safeArea;

            ChooseSide(this.sidePriority, targetScreenRect, selfSize, this.spacing, safeAreaScreenRect,
                out Vector2 chosenScreenPos);

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(self.parent as RectTransform, chosenScreenPos,
                    myCamera, out Vector3 worldPoint))
            {
                self.position = worldPoint;
            }
        }

        private static Camera CanvasCamera(Canvas canvas)
        {
            if (!canvas)
                return null;

            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private static Rect WorldCornersToScreenRect(RectTransform rectTransform, Camera camera)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            return new Rect(min, max - min);
        }

        private static Vector2 ComputeCandidateScreenPosition(UIAnchorSide side, Rect targetScreenRect,
            Vector2 selfSize, float spacing)
        {
            Vector2 center = targetScreenRect.center;
            return side switch
            {
                UIAnchorSide.Above => new Vector2(center.x, targetScreenRect.yMax + spacing + selfSize.y * 0.5f),
                UIAnchorSide.Below => new Vector2(center.x, targetScreenRect.yMin - spacing - selfSize.y * 0.5f),
                UIAnchorSide.Right => new Vector2(targetScreenRect.xMax + spacing + selfSize.x * 0.5f, center.y),
                UIAnchorSide.Left => new Vector2(targetScreenRect.xMin - spacing - selfSize.x * 0.5f, center.y),
                _ => center,
            };
        }

        private static bool FitsWithin(Rect candidate, Rect bounds) =>
            candidate.xMin >= bounds.xMin && candidate.xMax <= bounds.xMax &&
            candidate.yMin >= bounds.yMin && candidate.yMax <= bounds.yMax;

        private static Vector2 ClampIntoRect(Vector2 center, Vector2 size, Rect bounds)
        {
            Vector2 half = size * 0.5f;
            float x = Mathf.Clamp(center.x, bounds.xMin + half.x,
                Mathf.Max(bounds.xMin + half.x, bounds.xMax - half.x));
            float y = Mathf.Clamp(center.y, bounds.yMin + half.y,
                Mathf.Max(bounds.yMin + half.y, bounds.yMax - half.y));
            return new Vector2(x, y);
        }

        /// <summary>The actual side-selection + clamp algorithm, factored out as pure math (no
        /// Canvas/Camera dependency) so Reposition and tests share one implementation - see
        /// AssemblyInfo.cs InternalsVisibleTo for test access.</summary>
        internal static UIAnchorSide ChooseSide(
            IReadOnlyList<UIAnchorSide> priority, Rect targetScreenRect, Vector2 selfSize, float spacing,
            Rect boundsScreenRect,
            out Vector2 screenPosition)
        {
            // Ruling (plan doesn't say what happens when no side fits): fall back to the
            // first/highest-priority side rather than the last, then let the clamp below pull
            // it back on screen - "prefer the most-wanted side, just keep it visible" instead
            // of an arbitrary pick from the bottom of the list.
            UIAnchorSide chosen = priority.Count > 0 ? priority[0] : UIAnchorSide.Below;
            Vector2 chosenPos = ComputeCandidateScreenPosition(chosen, targetScreenRect, selfSize, spacing);

            foreach (UIAnchorSide side in priority)
            {
                Vector2 candidate = ComputeCandidateScreenPosition(side, targetScreenRect, selfSize, spacing);
                Rect candidateRect = new Rect(candidate - selfSize * 0.5f, selfSize);
                if (FitsWithin(candidateRect, boundsScreenRect))
                {
                    chosen = side;
                    chosenPos = candidate;
                    break;
                }
            }

            screenPosition = ClampIntoRect(chosenPos, selfSize, boundsScreenRect);
            return chosen;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.rectTransform)
                this.rectTransform = (RectTransform)this.transform;
        }
#endif
    }
}
