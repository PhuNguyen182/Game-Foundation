using System;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Binding
{
    /// <summary>
    /// Binds an Observable&lt;T&gt; so every new value tweens the target smoothly instead of
    /// snapping (UIBinder's plain one-way binds, e.g. b.Fill/b.Color, write the new value
    /// immediately). Driven by UIMotionRunner.CreateTween/Retarget - the same PlayerLoop
    /// tick, unscaled time and ease table every UIMotion track uses, without needing a
    /// UIMotion component or DOTween (REWRITE_PLAN.md 2.7: no tween library dependency in
    /// UISystem).
    ///
    /// Each binding allocates exactly one UIValueTweenHandle&lt;T&gt; up front and reuses it
    /// for every subsequent value - real gameplay values (currency, HP, a combo counter)
    /// can change many times per frame, and re-allocating a tween object on every change
    /// would mean GC pressure scaling with how often the source fires, not with how many
    /// bindings exist. Retarget cuts the in-flight tween off in place (no snap back to its
    /// start) and starts a new one from the target's current value - the same
    /// "useStartValue = false" continuity UIMotion's Hide-cuts-Show uses, chosen so a rapid
    /// stream of values never fights itself or jumps.
    ///
    /// Every overload comes in an ease-type flavor and a curve flavor rather than one
    /// signature taking both - the two are mutually exclusive ways to shape the same [0,1]
    /// progress, and a single "pass both, curve wins if non-null" signature reads as if
    /// they could be combined.
    ///
    /// Every target-specific helper (TweenFill/TweenColor/...) also comes in an
    /// Observable&lt;T&gt; flavor and a ReactiveProperty&lt;T&gt; flavor. Since ReactiveProperty&lt;T&gt;
    /// already implements Observable&lt;T&gt;, the Observable&lt;T&gt; overload would technically
    /// accept one - but it would then read the *target*'s current value on every change
    /// (via readCurrent, e.g. target.fillAmount) instead of the property's own .Value,
    /// missing the whole reason to expose ReactiveProperty&lt;T&gt; specifically (see
    /// BindTweenFromValue's remarks). The ReactiveProperty&lt;T&gt; overload always wins
    /// overload resolution for a ReactiveProperty&lt;T&gt; argument (more specific parameter
    /// type), so this is not just documentation - callers get the seeded-from-.Value path
    /// automatically without needing to pick the right overload themselves.
    /// </summary>
    public static class UIBinderTweenExtensions
    {
        // ---- Escape hatch: any value, caller supplies the setter (and optionally how to lerp it) ----

        public static void TweenValue<T>(
            this ref UIBinder b, Observable<T> source, float duration, UIEaseType ease,
            Action<T> setter, Func<T, T, float, T> lerp = null) =>
            BindTween(ref b, source, duration, t => UIEase.Evaluate(ease, t), setter, ResolveLerp(lerp));

        public static void TweenValue<T>(
            this ref UIBinder b, Observable<T> source, float duration, AnimationCurve curve,
            Action<T> setter, Func<T, T, float, T> lerp = null) =>
            BindTween(ref b, source, duration, RequireCurve(curve), setter, ResolveLerp(lerp));

        /// <summary>ReactiveProperty&lt;T&gt; overload of the escape hatch: unlike a plain
        /// Observable&lt;T&gt;, a ReactiveProperty always already holds a value, so the very
        /// first tween can start from source.Value instead of having nowhere to tween from
        /// on the first emission. Reads source.Value once, before Subscribe, not on every
        /// emission - ReactiveProperty sets Value before it notifies subscribers, so
        /// re-reading it from inside the Subscribe callback would already see the new
        /// value (from == to, nothing to tween). From then on this tracks the same running
        /// "current" a plain Observable&lt;T&gt; TweenValue does.</summary>
        public static void TweenValue<T>(
            this ref UIBinder b, ReactiveProperty<T> source, float duration, UIEaseType ease,
            Action<T> setter, Func<T, T, float, T> lerp = null) =>
            BindTweenFromValue(ref b, source, duration, t => UIEase.Evaluate(ease, t), setter, ResolveLerp(lerp));

        public static void TweenValue<T>(
            this ref UIBinder b, ReactiveProperty<T> source, float duration, AnimationCurve curve,
            Action<T> setter, Func<T, T, float, T> lerp = null) =>
            BindTweenFromValue(ref b, source, duration, RequireCurve(curve), setter, ResolveLerp(lerp));

        // ---- Image.fillAmount ----

        public static void TweenFill(
            this ref UIBinder b, Image target, Observable<float> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTween(ref b, source, duration, t => UIEase.Evaluate(ease, t), v => target.fillAmount = v, LerpFloat,
                () => target.fillAmount);

        public static void TweenFill(
            this ref UIBinder b, Image target, Observable<float> source, float duration, AnimationCurve curve) =>
            BindTween(ref b, source, duration, RequireCurve(curve), v => target.fillAmount = v, LerpFloat,
                () => target.fillAmount);

        public static void TweenFill(
            this ref UIBinder b, Image target, ReactiveProperty<float> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTweenFromValue(ref b, source, duration, t => UIEase.Evaluate(ease, t), v => target.fillAmount = v,
                LerpFloat);

        public static void TweenFill(
            this ref UIBinder b, Image target, ReactiveProperty<float> source, float duration,
            AnimationCurve curve) =>
            BindTweenFromValue(ref b, source, duration, RequireCurve(curve), v => target.fillAmount = v, LerpFloat);

        // ---- Graphic.color ----

        public static void TweenColor(
            this ref UIBinder b, Graphic target, Observable<Color> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTween(ref b, source, duration, t => UIEase.Evaluate(ease, t), v => target.color = v, LerpColor,
                () => target.color);

        public static void TweenColor(
            this ref UIBinder b, Graphic target, Observable<Color> source, float duration, AnimationCurve curve) =>
            BindTween(ref b, source, duration, RequireCurve(curve), v => target.color = v, LerpColor,
                () => target.color);

        public static void TweenColor(
            this ref UIBinder b, Graphic target, ReactiveProperty<Color> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTweenFromValue(ref b, source, duration, t => UIEase.Evaluate(ease, t), v => target.color = v,
                LerpColor);

        public static void TweenColor(
            this ref UIBinder b, Graphic target, ReactiveProperty<Color> source, float duration,
            AnimationCurve curve) =>
            BindTweenFromValue(ref b, source, duration, RequireCurve(curve), v => target.color = v, LerpColor);

        // ---- RectTransform.anchoredPosition ----

        public static void TweenAnchoredPosition(
            this ref UIBinder b, RectTransform target, Observable<Vector2> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTween(ref b, source, duration, t => UIEase.Evaluate(ease, t), v => target.anchoredPosition = v,
                LerpVector2, () => target.anchoredPosition);

        public static void TweenAnchoredPosition(
            this ref UIBinder b, RectTransform target, Observable<Vector2> source, float duration,
            AnimationCurve curve) =>
            BindTween(ref b, source, duration, RequireCurve(curve), v => target.anchoredPosition = v, LerpVector2,
                () => target.anchoredPosition);

        public static void TweenAnchoredPosition(
            this ref UIBinder b, RectTransform target, ReactiveProperty<Vector2> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTweenFromValue(ref b, source, duration, t => UIEase.Evaluate(ease, t),
                v => target.anchoredPosition = v, LerpVector2);

        public static void TweenAnchoredPosition(
            this ref UIBinder b, RectTransform target, ReactiveProperty<Vector2> source, float duration,
            AnimationCurve curve) =>
            BindTweenFromValue(ref b, source, duration, RequireCurve(curve), v => target.anchoredPosition = v,
                LerpVector2);

        // ---- Transform.localScale ----

        public static void TweenLocalScale(
            this ref UIBinder b, Transform target, Observable<Vector3> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTween(ref b, source, duration, t => UIEase.Evaluate(ease, t), v => target.localScale = v,
                LerpVector3, () => target.localScale);

        public static void TweenLocalScale(
            this ref UIBinder b, Transform target, Observable<Vector3> source, float duration,
            AnimationCurve curve) =>
            BindTween(ref b, source, duration, RequireCurve(curve), v => target.localScale = v, LerpVector3,
                () => target.localScale);

        public static void TweenLocalScale(
            this ref UIBinder b, Transform target, ReactiveProperty<Vector3> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTweenFromValue(ref b, source, duration, t => UIEase.Evaluate(ease, t), v => target.localScale = v,
                LerpVector3);

        public static void TweenLocalScale(
            this ref UIBinder b, Transform target, ReactiveProperty<Vector3> source, float duration,
            AnimationCurve curve) =>
            BindTweenFromValue(ref b, source, duration, RequireCurve(curve), v => target.localScale = v,
                LerpVector3);

        // ---- Transform.localRotation ----

        public static void TweenLocalRotation(
            this ref UIBinder b, Transform target, Observable<Quaternion> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTween(ref b, source, duration, t => UIEase.Evaluate(ease, t), v => target.localRotation = v,
                LerpQuaternion, () => target.localRotation);

        public static void TweenLocalRotation(
            this ref UIBinder b, Transform target, Observable<Quaternion> source, float duration,
            AnimationCurve curve) =>
            BindTween(ref b, source, duration, RequireCurve(curve), v => target.localRotation = v, LerpQuaternion,
                () => target.localRotation);

        public static void TweenLocalRotation(
            this ref UIBinder b, Transform target, ReactiveProperty<Quaternion> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad) =>
            BindTweenFromValue(ref b, source, duration, t => UIEase.Evaluate(ease, t),
                v => target.localRotation = v, LerpQuaternion);

        public static void TweenLocalRotation(
            this ref UIBinder b, Transform target, ReactiveProperty<Quaternion> source, float duration,
            AnimationCurve curve) =>
            BindTweenFromValue(ref b, source, duration, RequireCurve(curve), v => target.localRotation = v,
                LerpQuaternion);

        // ---- TMP_Text count-up (int) ----

        public static void TweenText(
            this ref UIBinder b, TMP_Text target, Observable<int> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad, string format = null) =>
            BindTweenText(ref b, target, source, duration, t => UIEase.Evaluate(ease, t), format);

        public static void TweenText(
            this ref UIBinder b, TMP_Text target, Observable<int> source, float duration, AnimationCurve curve,
            string format = null) =>
            BindTweenText(ref b, target, source, duration, RequireCurve(curve), format);

        public static void TweenText(
            this ref UIBinder b, TMP_Text target, ReactiveProperty<int> source, float duration,
            UIEaseType ease = UIEaseType.OutQuad, string format = null) =>
            BindTweenTextFromValue(ref b, target, source, duration, t => UIEase.Evaluate(ease, t), format);

        public static void TweenText(
            this ref UIBinder b, TMP_Text target, ReactiveProperty<int> source, float duration, AnimationCurve curve,
            string format = null) =>
            BindTweenTextFromValue(ref b, target, source, duration, RequireCurve(curve), format);

        /// <summary>Not routed through the readCurrent-based BindTween overload: the only
        /// place to read "the current value" back would be re-parsing target.text, which
        /// breaks the moment `format` renders it as anything int.Parse can't round-trip
        /// (e.g. "N0" -> "1,234"). Tracks the current int in the closure instead, same as
        /// the plain-setter TweenValue overload does. One CreateTween call up front, then
        /// Retarget on every emission - not a new tween per value.</summary>
        private static void BindTweenText(
            ref UIBinder b, TMP_Text target, Observable<int> source, float duration, Func<float, float> ease,
            string format)
        {
            int current = 0;
            bool hasCurrent = false;

            UIMotionRunner.UIValueTweenHandle<int> tween = UIMotionRunner.CreateTween<int>(duration, ease, LerpInt, v =>
            {
                current = v;
                target.SetText(FormatInt(v, format));
            });

            IDisposable subscription = source.Subscribe(value =>
            {
                int from = hasCurrent ? current : value;
                hasCurrent = true;
                tween.Retarget(from, value);
            });

            b.Add(subscription);
            b.Add(tween);
        }

        /// <summary>ReactiveProperty&lt;int&gt; counterpart of BindTweenText - seeds `current`
        /// from source.Value once, before Subscribe, for the same reason
        /// BindTweenFromValue does for every other type (see its remarks). Kept separate
        /// from BindTweenFromValue because TweenText's setter needs the `format` string,
        /// not just a plain Action&lt;int&gt;.</summary>
        private static void BindTweenTextFromValue(
            ref UIBinder b, TMP_Text target, ReactiveProperty<int> source, float duration, Func<float, float> ease,
            string format)
        {
            int current = source.Value;

            UIMotionRunner.UIValueTweenHandle<int> tween = UIMotionRunner.CreateTween<int>(duration, ease, LerpInt, v =>
            {
                current = v;
                target.SetText(FormatInt(v, format));
            });

            IDisposable subscription = source.Subscribe(value => tween.Retarget(current, value));

            b.Add(subscription);
            b.Add(tween);
        }

        // ---- Shared plumbing ----

        /// <summary>Overload used by TweenValue: no way to read "the target's current
        /// value" back for a plain caller-supplied setter, so every new value tweens from
        /// the previous value passed to this same call chain (starting at `source`'s first
        /// emission, which has nothing to tween from and applies instantly).</summary>
        private static void BindTween<T>(
            ref UIBinder b, Observable<T> source, float duration, Func<float, float> ease, Action<T> setter,
            Func<T, T, float, T> lerp)
        {
            T current = default;
            bool hasCurrent = false;

            UIMotionRunner.UIValueTweenHandle<T> tween = UIMotionRunner.CreateTween(duration, ease, lerp, v =>
            {
                current = v;
                setter(v);
            });

            IDisposable subscription = source.Subscribe(value =>
            {
                T from = hasCurrent ? current : value;
                hasCurrent = true;
                tween.Retarget(from, value);
            });

            b.Add(subscription);
            b.Add(tween);
        }

        /// <summary>Overload used by every ReactiveProperty&lt;T&gt; overload above
        /// (TweenValue, TweenFill, TweenColor, TweenAnchoredPosition, TweenLocalScale,
        /// TweenLocalRotation): seeds `current` from source.Value once, before Subscribe,
        /// instead of waiting for the first emission to have nowhere to tween from.
        /// ReactiveProperty sets Value before it notifies subscribers, so re-reading it
        /// from inside the Subscribe callback would already see the new value (from == to,
        /// nothing to tween) - this is why ReactiveProperty&lt;T&gt; gets its own overload
        /// instead of always going through the readCurrent-based BindTween below, even for
        /// the target-specific helpers that could otherwise read the target's own current
        /// value.</summary>
        private static void BindTweenFromValue<T>(
            ref UIBinder b, ReactiveProperty<T> source, float duration, Func<float, float> ease, Action<T> setter,
            Func<T, T, float, T> lerp)
        {
            T current = source.Value;

            UIMotionRunner.UIValueTweenHandle<T> tween = UIMotionRunner.CreateTween(duration, ease, lerp, v =>
            {
                current = v;
                setter(v);
            });

            IDisposable subscription = source.Subscribe(value => tween.Retarget(current, value));

            b.Add(subscription);
            b.Add(tween);
        }

        /// <summary>Overload used by the target-specific helpers' Observable&lt;T&gt;
        /// flavor (TweenFill/TweenColor/...): they can read the target's live value, so
        /// the very first emission also tweens in from whatever the target is currently
        /// showing instead of snapping.</summary>
        private static void BindTween<T>(
            ref UIBinder b, Observable<T> source, float duration, Func<float, float> ease, Action<T> setter,
            Func<T, T, float, T> lerp, Func<T> readCurrent)
        {
            UIMotionRunner.UIValueTweenHandle<T> tween = UIMotionRunner.CreateTween(duration, ease, lerp, setter);

            IDisposable subscription = source.Subscribe(value => tween.Retarget(readCurrent(), value));

            b.Add(subscription);
            b.Add(tween);
        }

        private static Func<float, float> RequireCurve(AnimationCurve curve)
        {
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));
            return curve.Evaluate;
        }

        private static Func<T, T, float, T> ResolveLerp<T>(Func<T, T, float, T> lerp) => lerp ?? DefaultLerp<T>();

        private static Func<T, T, float, T> DefaultLerp<T>()
        {
            if (typeof(T) == typeof(float))
            {
                Func<float, float, float, float> f = LerpFloat;
                return (Func<T, T, float, T>)(Delegate)f;
            }

            if (typeof(T) == typeof(int))
            {
                Func<int, int, float, int> f = LerpInt;
                return (Func<T, T, float, T>)(Delegate)f;
            }

            if (typeof(T) == typeof(Color))
            {
                Func<Color, Color, float, Color> f = LerpColor;
                return (Func<T, T, float, T>)(Delegate)f;
            }

            if (typeof(T) == typeof(Vector2))
            {
                Func<Vector2, Vector2, float, Vector2> f = LerpVector2;
                return (Func<T, T, float, T>)(Delegate)f;
            }

            if (typeof(T) == typeof(Vector3))
            {
                Func<Vector3, Vector3, float, Vector3> f = LerpVector3;
                return (Func<T, T, float, T>)(Delegate)f;
            }

            if (typeof(T) == typeof(Quaternion))
            {
                Func<Quaternion, Quaternion, float, Quaternion> f = LerpQuaternion;
                return (Func<T, T, float, T>)(Delegate)f;
            }

            throw new ArgumentException(
                $"UIBinder.TweenValue<{typeof(T).Name}>: no default lerp for this type - pass one explicitly.");
        }

        private static float LerpFloat(float a, float b, float t) => Mathf.LerpUnclamped(a, b, t);
        private static int LerpInt(int a, int b, float t) => Mathf.RoundToInt(Mathf.LerpUnclamped(a, b, t));
        private static Color LerpColor(Color a, Color b, float t) => Color.LerpUnclamped(a, b, t);
        private static Vector2 LerpVector2(Vector2 a, Vector2 b, float t) => Vector2.LerpUnclamped(a, b, t);
        private static Vector3 LerpVector3(Vector3 a, Vector3 b, float t) => Vector3.LerpUnclamped(a, b, t);

        /// <summary>Slerp, not Lerp: constant angular velocity along the shortest arc, the
        /// correct interpolation for rotation regardless of how large the angle between a
        /// and b is - a linear Lerp on a Quaternion's raw components speeds up/slows down
        /// unevenly and only stays reasonable for small angles.</summary>
        private static Quaternion LerpQuaternion(Quaternion a, Quaternion b, float t) =>
            Quaternion.SlerpUnclamped(a, b, t);

        private static string FormatInt(int value, string format) =>
            format == null ? value.ToString() : value.ToString(format);
    }
}
