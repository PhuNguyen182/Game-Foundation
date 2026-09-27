using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// Per-kind capture/write, dispatched through a switch (not a virtual call or a
    /// dictionary of delegates) so ticking many tracks does not allocate or box.
    /// AnimatorState has no evaluator here yet - deliberately absent from both switches
    /// and falling through to a no-op, so an unfinished kind never throws, it is simply
    /// inert until its evaluator lands. Custom bypasses Capture/Write entirely (see
    /// TryGetCustomTrack + UIMotionRunner.TickCustomTrack) since it owns its own values
    /// through IUIMotionCustomTrack rather than a Vector4. Rect dispatches all 8
    /// UIMotionRectProperty options through CaptureRect/WriteRect below, including the
    /// pivot/anchor "keep visual position" compensation (WritePivot/WriteAnchors).
    /// </summary>
    internal static class UIMotionTrackEvaluator
    {
        /// <summary>Number of full sine cycles Punch/Shake complete over their duration.
        /// Not exposed as a field on UIMotionTrack (the plan only calls out "chỉ có biên
        /// độ" - amplitude-only), so this is a ruling: fixed until there's an Inspector to
        /// preview and tune it by eye.</summary>
        private const float OscillationCycles = 3f;

        public static Vector4 Capture(UIMotionTrack track)
        {
            switch (track.kind)
            {
                case UIMotionTrackKind.Fade:
                    return As<CanvasGroup>(track.target, out CanvasGroup cg) ? new Vector4(cg.alpha, 0f, 0f, 0f) : Vector4.zero;

                case UIMotionTrackKind.Move:
                    return AsRectTransform(track.target, out RectTransform rtMove) ? (Vector4)(Vector2)rtMove.anchoredPosition : Vector4.zero;

                case UIMotionTrackKind.Scale:
                case UIMotionTrackKind.Punch:
                    return AsTransform(track.target, out Transform tScale) ? (Vector4)tScale.localScale : Vector4.zero;

                case UIMotionTrackKind.Rotate:
                    return AsTransform(track.target, out Transform tRotate) ? (Vector4)tRotate.localEulerAngles : Vector4.zero;

                case UIMotionTrackKind.Color:
                    return As<Graphic>(track.target, out Graphic graphic) ? (Vector4)graphic.color : Vector4.zero;

                case UIMotionTrackKind.Fill:
                    return As<Image>(track.target, out Image image) ? new Vector4(image.fillAmount, 0f, 0f, 0f) : Vector4.zero;

                case UIMotionTrackKind.Shake:
                    return AsRectTransform(track.target, out RectTransform rtShake) ? (Vector4)(Vector2)rtShake.anchoredPosition : Vector4.zero;

                case UIMotionTrackKind.SetActive:
                    return AsGameObject(track.target, out GameObject go) ? new Vector4(go.activeSelf ? 1f : 0f, 0f, 0f, 0f) : Vector4.zero;

                case UIMotionTrackKind.Rect:
                    return CaptureRect(track);

                default:
                    return Vector4.zero;
            }
        }

        private static Vector4 CaptureRect(UIMotionTrack track)
        {
            if (!AsRectTransform(track.target, out RectTransform rt))
                return Vector4.zero;

            switch (track.rectProperty)
            {
                case UIMotionRectProperty.AnchoredPosition: return (Vector4)(Vector2)rt.anchoredPosition;
                case UIMotionRectProperty.AnchorMin: return (Vector4)(Vector2)rt.anchorMin;
                case UIMotionRectProperty.AnchorMax: return (Vector4)(Vector2)rt.anchorMax;
                case UIMotionRectProperty.Anchors: return new Vector4(rt.anchorMin.x, rt.anchorMin.y, rt.anchorMax.x, rt.anchorMax.y);
                case UIMotionRectProperty.Pivot: return (Vector4)(Vector2)rt.pivot;
                case UIMotionRectProperty.SizeDelta: return (Vector4)(Vector2)rt.sizeDelta;
                case UIMotionRectProperty.OffsetMin: return (Vector4)(Vector2)rt.offsetMin;
                case UIMotionRectProperty.OffsetMax: return (Vector4)(Vector2)rt.offsetMax;
                default: return Vector4.zero;
            }
        }

        public static void Write(UIMotionTrack track, Vector4 value)
        {
            switch (track.kind)
            {
                case UIMotionTrackKind.Fade:
                    if (As<CanvasGroup>(track.target, out CanvasGroup cg))
                        cg.alpha = value.x;
                    break;

                case UIMotionTrackKind.Move:
                    if (AsRectTransform(track.target, out RectTransform rtMove))
                        rtMove.anchoredPosition = value;
                    break;

                case UIMotionTrackKind.Scale:
                case UIMotionTrackKind.Punch:
                    if (AsTransform(track.target, out Transform tScale))
                        tScale.localScale = value;
                    break;

                case UIMotionTrackKind.Rotate:
                    if (AsTransform(track.target, out Transform tRotate))
                        tRotate.localEulerAngles = value;
                    break;

                case UIMotionTrackKind.Color:
                    if (As<Graphic>(track.target, out Graphic graphic))
                        graphic.color = value;
                    break;

                case UIMotionTrackKind.Fill:
                    if (As<Image>(track.target, out Image image))
                        image.fillAmount = value.x;
                    break;

                case UIMotionTrackKind.Shake:
                    if (AsRectTransform(track.target, out RectTransform rtShake))
                        rtShake.anchoredPosition = value;
                    break;

                case UIMotionTrackKind.SetActive:
                    // Discrete, not interpolated. UIMotionRunner fires this exactly once,
                    // the moment the track starts - never on every tick - so value.x is
                    // always either 0 or 1 here (see UIMotionRunner.TickTracks).
                    if (AsGameObject(track.target, out GameObject go))
                        go.SetActive(value.x > 0.5f);
                    break;

                case UIMotionTrackKind.Rect:
                    WriteRect(track, value);
                    break;
            }
        }

        private static void WriteRect(UIMotionTrack track, Vector4 value)
        {
            if (!AsRectTransform(track.target, out RectTransform rt))
                return;

            switch (track.rectProperty)
            {
                case UIMotionRectProperty.AnchoredPosition:
                    rt.anchoredPosition = value;
                    break;

                case UIMotionRectProperty.SizeDelta:
                    rt.sizeDelta = value;
                    break;

                case UIMotionRectProperty.OffsetMin:
                    rt.offsetMin = value;
                    break;

                case UIMotionRectProperty.OffsetMax:
                    rt.offsetMax = value;
                    break;

                case UIMotionRectProperty.Pivot:
                    WritePivot(rt, value, track.preserveVisualPosition);
                    break;

                case UIMotionRectProperty.AnchorMin:
                    WriteAnchors(rt, value, rt.anchorMax, track.preserveVisualPosition);
                    break;

                case UIMotionRectProperty.AnchorMax:
                    WriteAnchors(rt, rt.anchorMin, value, track.preserveVisualPosition);
                    break;

                case UIMotionRectProperty.Anchors:
                    WriteAnchors(rt, new Vector2(value.x, value.y), new Vector2(value.z, value.w), track.preserveVisualPosition);
                    break;
            }
        }

        /// <summary>REWRITE_PLAN.md 2.7: "Đổi Pivot thì tự bù anchoredPosition += (pivotMới
        /// − pivotCũ) × size, để object không nhảy chỗ" - changing pivot alone moves the
        /// rect (anchoredPosition is defined relative to the pivot point), so this cancels
        /// that side effect. Off reproduces the jump.</summary>
        private static void WritePivot(RectTransform rt, Vector2 newPivot, bool preserveVisualPosition)
        {
            if (!preserveVisualPosition)
            {
                rt.pivot = newPivot;
                return;
            }

            Vector2 oldPivot = rt.pivot;
            Vector2 size = rt.rect.size;
            rt.pivot = newPivot;
            rt.anchoredPosition += (newPivot - oldPivot) * size;
        }

        /// <summary>
        /// REWRITE_PLAN.md 2.7: "Đổi AnchorMin/Max/Anchors thì tự bù offset để rect giữ
        /// nguyên trên màn hình, giống cách Inspector của Unity làm" - with this on
        /// (default), animating anchors re-anchors the rect without moving it, same as
        /// Unity's own anchor-preset buttons; "tắt tuỳ chọn này nếu muốn object dịch
        /// chuyển theo anchor" - off, it moves (the old MoveAnchorAnimation behavior).
        /// Recomputed from the CURRENT offsets every write (not from a rest pose), so
        /// chaining writes across many ticks preserves the on-screen edges throughout the
        /// whole track, not just at its endpoints.
        /// </summary>
        private static void WriteAnchors(RectTransform rt, Vector2 newAnchorMin, Vector2 newAnchorMax, bool preserveVisualPosition)
        {
            if (!preserveVisualPosition)
            {
                rt.anchorMin = newAnchorMin;
                rt.anchorMax = newAnchorMax;
                return;
            }

            Vector2 parentSize = GetParentSizeRaw(rt);
            Vector2 oldAnchorMin = rt.anchorMin;
            Vector2 oldAnchorMax = rt.anchorMax;
            Vector2 oldOffsetMin = rt.offsetMin;
            Vector2 oldOffsetMax = rt.offsetMax;

            float leftEdge = oldAnchorMin.x * parentSize.x + oldOffsetMin.x;
            float rightEdge = oldAnchorMax.x * parentSize.x + oldOffsetMax.x;
            float bottomEdge = oldAnchorMin.y * parentSize.y + oldOffsetMin.y;
            float topEdge = oldAnchorMax.y * parentSize.y + oldOffsetMax.y;

            rt.anchorMin = newAnchorMin;
            rt.anchorMax = newAnchorMax;

            rt.offsetMin = new Vector2(leftEdge - newAnchorMin.x * parentSize.x, bottomEdge - newAnchorMin.y * parentSize.y);
            rt.offsetMax = new Vector2(rightEdge - newAnchorMax.x * parentSize.x, topEdge - newAnchorMax.y * parentSize.y);
        }

        /// <summary>
        /// Punch/Shake's value at normalized time t: a sine wave around `basePose`
        /// (captured live, same as any non-useStartValue track) whose amplitude decays
        /// linearly to exactly 0 at t=1 - REWRITE_PLAN.md 2.7: "Punch và Shake luôn dao
        /// động quanh giá trị hiện tại rồi trả về đúng giá trị đó". Punch (Transform.
        /// localScale) oscillates uniformly on all 3 axes, multiplicative like the Scale
        /// kind (amplitude 0.2 = punches up to 120% then decays back to 100%). Shake
        /// (RectTransform.anchoredPosition) oscillates x/y at two different frequencies
        /// (not a shared phase offset - that would make sin(0) != 0 on one axis, breaking
        /// "starts exactly at basePose" too, not just "ends" there) so the combined path
        /// reads as a wiggle instead of a single diagonal line.
        /// </summary>
        public static Vector4 EvaluateOscillation(UIMotionTrackKind kind, Vector4 basePose, float amplitude, float t)
        {
            float decay = 1f - Mathf.Clamp01(t);
            float wave = Mathf.Sin(t * OscillationCycles * Mathf.PI * 2f) * amplitude * decay;

            switch (kind)
            {
                case UIMotionTrackKind.Punch:
                {
                    float k = 1f + wave;
                    return new Vector4(basePose.x * k, basePose.y * k, basePose.z * k, basePose.w);
                }

                case UIMotionTrackKind.Shake:
                {
                    float waveY = Mathf.Sin(t * (OscillationCycles + 0.5f) * Mathf.PI * 2f) * amplitude * decay;
                    return new Vector4(basePose.x + wave, basePose.y + waveY, basePose.z, basePose.w);
                }

                default:
                    return basePose;
            }
        }

        /// <summary>
        /// Rewrites `to` so each axis is the shortest angular delta from `from`, instead
        /// of a raw Lerp between two [0,360) angles (which can spin the long way round -
        /// e.g. from 350 deg to 0 deg would otherwise travel 350 deg instead of 10 deg).
        /// Only meaningful for the Rotate kind.
        /// </summary>
        public static Vector4 UnwrapRotation(Vector4 from, Vector4 to) =>
            new Vector4(
                from.x + Mathf.DeltaAngle(from.x, to.x),
                from.y + Mathf.DeltaAngle(from.y, to.y),
                from.z + Mathf.DeltaAngle(from.z, to.z),
                to.w);

        /// <summary>Resolves `track.target` to the Animator it references - a direct
        /// reference, or (mirroring every other kind's fallback) a component lookup when
        /// the author dragged in a GameObject instead.</summary>
        public static bool TryGetAnimator(UIMotionTrack track, out Animator animator) =>
            As(track.target, out animator);

        /// <summary>Resolves `track.target` to the IUIMotionCustomTrack it references -
        /// a direct reference, or (mirroring every other kind's fallback) a component
        /// lookup when the author dragged in a GameObject or a different component.</summary>
        public static bool TryGetCustomTrack(UIMotionTrack track, out IUIMotionCustomTrack custom)
        {
            var direct = track.target as IUIMotionCustomTrack;
            if (direct != null)
            {
                custom = direct;
                return true;
            }

            var go = track.target as GameObject;
            if (go != null)
                return go.TryGetComponent(out custom);

            var component = track.target as Component;
            if (component != null)
                return component.TryGetComponent(out custom);

            custom = null;
            return false;
        }

        /// <summary>Direct child count of `track.target`'s Transform, for stagger
        /// expansion. Zero (never throws) if the target doesn't resolve to a Transform.</summary>
        public static int GetChildCount(UIMotionTrack track) =>
            AsTransform(track.target, out Transform t) ? t.childCount : 0;

        /// <summary>The index-th direct child of `track.target`'s Transform, as a
        /// GameObject - so a stagger clone's `target` still goes through the same
        /// GameObject/Component fallback resolution as any authored track.</summary>
        public static GameObject GetChild(UIMotionTrack track, int index) =>
            AsTransform(track.target, out Transform t) ? t.GetChild(index).gameObject : null;

        /// <summary>
        /// Size of the target's parent RectTransform, for FractionOfParent value mode.
        /// Only meaningful for RectTransform-based kinds (Move, and Rect once it lands);
        /// everything else resolves to zero, matching the plan's restriction of
        /// FractionOfParent to AnchoredPosition/SizeDelta/Offset.
        /// </summary>
        public static Vector4 GetParentSize(UIMotionTrack track)
        {
            if (!AsRectTransform(track.target, out RectTransform rt))
                return Vector4.zero;

            Vector2 size = GetParentSizeRaw(rt);
            return new Vector4(size.x, size.y, 0f, 0f);
        }

        private static Vector2 GetParentSizeRaw(RectTransform rt)
        {
            var parent = rt.parent as RectTransform;
            return parent != null ? parent.rect.size : Vector2.zero;
        }

        // RectTransform is also a Transform, so it needs its own resolver rather than
        // going through the generic As<T> (which would let a plain-Transform-typed track
        // silently accept a RectTransform's owning GameObject and then fail the cast).
        // Unity's "fake null" (a destroyed object whose C# wrapper is still a live,
        // non-null CLR reference) is only caught by UnityEngine.Object's overloaded
        // == / != operator - never by an `is`/`as` type pattern alone, and calling an
        // instance member on a fake-null reference throws MissingReferenceException. So
        // every helper below casts with `as` and then explicitly compares `!= null`
        // before touching the result, rather than using `is` pattern matching.

        private static bool AsRectTransform(Object target, out RectTransform rectTransform)
        {
            var direct = target as RectTransform;
            if (direct != null)
            {
                rectTransform = direct;
                return true;
            }

            var go = target as GameObject;
            if (go != null)
                return go.TryGetComponent(out rectTransform);

            var component = target as Component;
            if (component != null)
                return component.TryGetComponent(out rectTransform);

            rectTransform = null;
            return false;
        }

        private static bool AsTransform(Object target, out Transform transform)
        {
            var direct = target as Transform;
            if (direct != null)
            {
                transform = direct;
                return true;
            }

            var go = target as GameObject;
            if (go != null)
            {
                transform = go.transform;
                return true;
            }

            var component = target as Component;
            if (component != null)
            {
                transform = component.transform;
                return true;
            }

            transform = null;
            return false;
        }

        /// <summary>
        /// Resolves `target` to a component of type T: a direct reference, or - if the
        /// author dragged in the GameObject (or a different component on it) instead of
        /// the specific component the track kind needs - a TryGetComponent fallback, so
        /// a mismatched drag-and-drop is inert only when the component truly isn't there,
        /// not whenever the reference isn't of the exact expected type.
        /// </summary>
        private static bool As<T>(Object target, out T component) where T : Component
        {
            var direct = target as T;
            if (direct != null)
            {
                component = direct;
                return true;
            }

            var go = target as GameObject;
            if (go != null)
                return go.TryGetComponent(out component);

            var other = target as Component;
            if (other != null)
                return other.TryGetComponent(out component);

            component = null;
            return false;
        }

        private static bool AsGameObject(Object target, out GameObject gameObject)
        {
            var go = target as GameObject;
            if (go != null)
            {
                gameObject = go;
                return true;
            }

            var component = target as Component;
            if (component != null)
            {
                gameObject = component.gameObject;
                return true;
            }

            gameObject = null;
            return false;
        }
    }
}
