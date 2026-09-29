using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;

namespace DracoRuan.PrebuildServices.UISystem.Editor.MotionTools
{
    /// <summary>What kind of editor a track's raw Vector4 `from`/`to` should be drawn with.
    /// The runtime keeps one Vector4 for every kind; the Inspector shows the shape that kind
    /// actually reads.</summary>
    public enum UIMotionValueShape
    {
        None = 0,

        /// <summary>SetActive: `to.x` above 0.5 means active.</summary>
        Toggle = 1,

        /// <summary>Punch/Shake: `to.x` is the oscillation amplitude.</summary>
        Amplitude = 2,

        Float = 3,
        Vector2 = 4,
        Vector3 = 5,
        Color = 6,
        Vector4 = 7,
    }

    /// <summary>
    /// Which UIMotionTrack fields matter for which kind, so the Inspector can hide the rest.
    /// Pure (no IMGUI) so it can be unit-tested; mirrors what UIMotionTrackEvaluator and
    /// UIMotionRunner actually read per kind. Hidden fields keep their serialized values, so
    /// flipping `kind` back and forth never loses anything.
    /// </summary>
    public static class UIMotionTrackFieldVisibility
    {
        /// <summary>Kinds that lerp between a Start and a Target value.</summary>
        public static bool IsRange(UIMotionTrackKind kind) =>
            kind is UIMotionTrackKind.Fade or UIMotionTrackKind.Move or UIMotionTrackKind.Rect
                or UIMotionTrackKind.Scale or UIMotionTrackKind.Rotate or UIMotionTrackKind.Color
                or UIMotionTrackKind.Fill;

        /// <summary>SetActive fires once at its start time; it has no window to fill.</summary>
        public static bool ShowDuration(UIMotionTrackKind kind) => kind != UIMotionTrackKind.SetActive;

        /// <summary>Punch/Shake bypass Evaluate(); SetActive and AnimatorState never ease. Custom
        /// receives the eased t in Sample().</summary>
        public static bool ShowEase(UIMotionTrackKind kind) =>
            IsRange(kind) || kind == UIMotionTrackKind.Custom;

        public static bool ShowUseStartValue(UIMotionTrackKind kind) => IsRange(kind);

        public static bool ShowFromValue(UIMotionTrackKind kind, bool useStartValue) =>
            IsRange(kind) && useStartValue;

        public static bool ShowToValue(UIMotionTrackKind kind) => ToShape(kind, UIMotionRectProperty.AnchoredPosition) != UIMotionValueShape.None;

        /// <summary>Whether the Start/Target value-mode popups are shown at all.</summary>
        public static bool ShowValueModes(UIMotionTrackKind kind) => IsRange(kind);

        public static bool ShowRectProperty(UIMotionTrackKind kind) => kind == UIMotionTrackKind.Rect;

        public static bool ShowPreserveVisualPosition(UIMotionTrackKind kind, UIMotionRectProperty property) =>
            kind == UIMotionTrackKind.Rect
            && property is UIMotionRectProperty.Pivot or UIMotionRectProperty.AnchorMin
                or UIMotionRectProperty.AnchorMax or UIMotionRectProperty.Anchors;

        /// <summary>Stagger fans a track out over the target's children; a Custom track owns its
        /// own values through one component, so there is nothing to fan out.</summary>
        public static bool ShowStagger(UIMotionTrackKind kind) => kind != UIMotionTrackKind.Custom;

        public static bool ShowAnimatorFields(UIMotionTrackKind kind) => kind == UIMotionTrackKind.AnimatorState;

        /// <summary>Only position/size-like values can be a fraction of the parent's size.</summary>
        public static bool AllowsFractionOfParent(UIMotionTrackKind kind, UIMotionRectProperty property) =>
            kind == UIMotionTrackKind.Move
            || (kind == UIMotionTrackKind.Rect
                && property is UIMotionRectProperty.AnchoredPosition or UIMotionRectProperty.SizeDelta
                    or UIMotionRectProperty.OffsetMin or UIMotionRectProperty.OffsetMax);

        /// <summary>Shape of the Target value (`to`). Start uses the same shape for Range kinds.</summary>
        public static UIMotionValueShape ToShape(UIMotionTrackKind kind, UIMotionRectProperty property)
        {
            switch (kind)
            {
                case UIMotionTrackKind.Fade:
                case UIMotionTrackKind.Fill:
                    return UIMotionValueShape.Float;
                case UIMotionTrackKind.Move:
                    return UIMotionValueShape.Vector2;
                case UIMotionTrackKind.Rect:
                    return property == UIMotionRectProperty.Anchors ? UIMotionValueShape.Vector4 : UIMotionValueShape.Vector2;
                case UIMotionTrackKind.Scale:
                case UIMotionTrackKind.Rotate:
                    return UIMotionValueShape.Vector3;
                case UIMotionTrackKind.Color:
                    return UIMotionValueShape.Color;
                case UIMotionTrackKind.Punch:
                case UIMotionTrackKind.Shake:
                    return UIMotionValueShape.Amplitude;
                case UIMotionTrackKind.SetActive:
                    return UIMotionValueShape.Toggle;
                default:
                    return UIMotionValueShape.None;
            }
        }

        public static UIMotionValueShape FromShape(UIMotionTrackKind kind, UIMotionRectProperty property) =>
            IsRange(kind) ? ToShape(kind, property) : UIMotionValueShape.None;

        /// <summary>Value modes to offer in a popup. `RelativeToStart` is meaningless for `from`
        /// (the runner resolves it against rest there), so it is only offered for the Target.
        /// The returned array is in display order.</summary>
        public static UIMotionValueMode[] AllowedModes(
            UIMotionTrackKind kind, UIMotionRectProperty property, bool forTarget)
        {
            var modes = new System.Collections.Generic.List<UIMotionValueMode>
            {
                UIMotionValueMode.Absolute,
                UIMotionValueMode.RelativeToRest,
            };

            if (forTarget)
                modes.Add(UIMotionValueMode.RelativeToStart);
            if (AllowsFractionOfParent(kind, property))
                modes.Add(UIMotionValueMode.FractionOfParent);

            return modes.ToArray();
        }

        /// <summary>The components a kind can write to (UIMotionTrackEvaluator.Write); the target needs
        /// at least one, otherwise the track is a silent no-op, which the Inspector flags. Fade accepts
        /// a CanvasGroup or any Graphic (Image, TextMeshPro...). GameObject means "any target will do"
        /// (SetActive); empty means the kind has no fixed component (Custom is checked against
        /// IUIMotionCustomTrack instead).</summary>
        public static System.Type[] RequiredComponents(UIMotionTrackKind kind)
        {
            switch (kind)
            {
                case UIMotionTrackKind.Fade:
                    return new[] { typeof(UnityEngine.CanvasGroup), typeof(UnityEngine.UI.Graphic) };
                case UIMotionTrackKind.Move:
                case UIMotionTrackKind.Shake:
                case UIMotionTrackKind.Rect:
                    return new[] { typeof(UnityEngine.RectTransform) };
                case UIMotionTrackKind.Scale:
                case UIMotionTrackKind.Punch:
                case UIMotionTrackKind.Rotate:
                    return new[] { typeof(UnityEngine.Transform) };
                case UIMotionTrackKind.Color:
                    return new[] { typeof(UnityEngine.UI.Graphic) };
                case UIMotionTrackKind.Fill:
                    return new[] { typeof(UnityEngine.UI.Image) };
                case UIMotionTrackKind.AnimatorState:
                    return new[] { typeof(UnityEngine.Animator) };
                case UIMotionTrackKind.SetActive:
                    return new[] { typeof(UnityEngine.GameObject) };
                default:
                    return System.Array.Empty<System.Type>();
            }
        }

        public static string TargetLabel(UIMotionTrackKind kind)
        {
            switch (kind)
            {
                case UIMotionTrackKind.AnimatorState:
                    return "Animator";
                case UIMotionTrackKind.Custom:
                    return "Custom Track";
                default:
                    return "Target";
            }
        }

        public static string ToLabel(UIMotionTrackKind kind)
        {
            switch (kind)
            {
                case UIMotionTrackKind.Punch:
                case UIMotionTrackKind.Shake:
                    return "Amplitude";
                case UIMotionTrackKind.SetActive:
                    return "Active";
                default:
                    return "Target Value";
            }
        }
    }
}
