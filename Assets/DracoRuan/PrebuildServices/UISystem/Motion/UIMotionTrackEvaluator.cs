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
                    return AsCanvasGroup(track.target, out CanvasGroup cg) ? new Vector4(cg.alpha, 0f, 0f, 0f) : Vector4.zero;

                case UIMotionTrackKind.Move:
                    return AsRectTransform(track.target, out RectTransform rtMove) ? (Vector4)(Vector2)rtMove.anchoredPosition : Vector4.zero;

                case UIMotionTrackKind.Scale:
                    return AsTransform(track.target, out Transform tScale) ? (Vector4)tScale.localScale : Vector4.zero;

                case UIMotionTrackKind.Rotate:
                    return AsTransform(track.target, out Transform tRotate) ? (Vector4)tRotate.localEulerAngles : Vector4.zero;

                case UIMotionTrackKind.Color:
                    return AsGraphic(track.target, out Graphic graphic) ? (Vector4)graphic.color : Vector4.zero;

                case UIMotionTrackKind.Fill:
                    return AsImage(track.target, out Image image) ? new Vector4(image.fillAmount, 0f, 0f, 0f) : Vector4.zero;

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
                    if (AsCanvasGroup(track.target, out CanvasGroup cg))
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
                    if (AsGraphic(track.target, out Graphic graphic))
                        graphic.color = value;
                    break;

                case UIMotionTrackKind.Fill:
                    if (AsImage(track.target, out Image image))
                        image.fillAmount = value.x;
                    break;

                case UIMotionTrackKind.SetActive:
                    // Binary, not interpolated: any progress past the midpoint of the
                    // track's own [0,1] t flips it. UIMotionRunner calls Write once at
                    // t=1 for this kind rather than every tick (see PlayTimelineAsync).
                    if (AsGameObject(track.target, out GameObject go))
                        go.SetActive(value.x > 0.5f);
                    break;
            }
        }

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

        private static bool AsCanvasGroup(Object target, out CanvasGroup canvasGroup)
        {
            canvasGroup = target as CanvasGroup;
            return canvasGroup != null;
        }

        private static bool AsRectTransform(Object target, out RectTransform rectTransform)
        {
            rectTransform = target as RectTransform;
            return rectTransform != null;
        }

        private static bool AsTransform(Object target, out Transform transform)
        {
            transform = target as Transform;
            return transform != null;
        }

        private static bool AsGraphic(Object target, out Graphic graphic)
        {
            graphic = target as Graphic;
            return graphic != null;
        }

        private static bool AsImage(Object target, out Image image)
        {
            image = target as Image;
            return image != null;
        }

        private static bool AsGameObject(Object target, out GameObject gameObject)
        {
            switch (target)
            {
                case GameObject go:
                    gameObject = go;
                    return true;
                case Component component:
                    gameObject = component.gameObject;
                    return true;
                default:
                    gameObject = null;
                    return false;
            }
        }
    }
}
