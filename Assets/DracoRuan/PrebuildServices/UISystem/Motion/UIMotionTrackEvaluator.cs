using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// Per-kind capture/write, dispatched through a switch (not a virtual call or a
    /// dictionary of delegates) so ticking many tracks does not allocate or box.
    /// Kinds without an evaluator here yet (Rect/Punch/Shake/AnimatorState/Custom) are
    /// deliberately absent from both switches and fall through to a no-op, so an
    /// unfinished kind never throws - it is simply inert until its evaluator lands.
    /// </summary>
    internal static class UIMotionTrackEvaluator
    {
        public static Vector4 Capture(UIMotionTrack track)
        {
            switch (track.kind)
            {
                case UIMotionTrackKind.Fade:
                    return As<CanvasGroup>(track.target, out CanvasGroup cg) ? new Vector4(cg.alpha, 0f, 0f, 0f) : Vector4.zero;

                case UIMotionTrackKind.Move:
                    return AsRectTransform(track.target, out RectTransform rtMove) ? (Vector4)(Vector2)rtMove.anchoredPosition : Vector4.zero;

                case UIMotionTrackKind.Scale:
                    return AsTransform(track.target, out Transform tScale) ? (Vector4)tScale.localScale : Vector4.zero;

                case UIMotionTrackKind.Rotate:
                    return AsTransform(track.target, out Transform tRotate) ? (Vector4)tRotate.localEulerAngles : Vector4.zero;

                case UIMotionTrackKind.Color:
                    return As<Graphic>(track.target, out Graphic graphic) ? (Vector4)graphic.color : Vector4.zero;

                case UIMotionTrackKind.Fill:
                    return As<Image>(track.target, out Image image) ? new Vector4(image.fillAmount, 0f, 0f, 0f) : Vector4.zero;

                case UIMotionTrackKind.SetActive:
                    return AsGameObject(track.target, out GameObject go) ? new Vector4(go.activeSelf ? 1f : 0f, 0f, 0f, 0f) : Vector4.zero;

                default:
                    return Vector4.zero;
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

                case UIMotionTrackKind.SetActive:
                    // Discrete, not interpolated. UIMotionRunner fires this exactly once,
                    // the moment the track starts - never on every tick - so value.x is
                    // always either 0 or 1 here (see UIMotionRunner.TickTracks).
                    if (AsGameObject(track.target, out GameObject go))
                        go.SetActive(value.x > 0.5f);
                    break;
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

            var parent = rt.parent as RectTransform;
            if (parent == null)
                return Vector4.zero;

            Vector2 size = parent.rect.size;
            return new Vector4(size.x, size.y, 0f, 0f);
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
