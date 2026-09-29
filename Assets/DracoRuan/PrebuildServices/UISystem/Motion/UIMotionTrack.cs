using System;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// One track inside a UIMotion timeline. Plain serializable class (no
    /// [SerializeReference]) so renaming a kind or an IL2CPP strip can never lose data;
    /// the inspector drawer shows only the fields relevant to `kind`.
    /// </summary>
    [Serializable]
    public class UIMotionTrack
    {
        [SerializeField] public UIMotionTrackKind kind = UIMotionTrackKind.Fade;

        /// <summary>What this track animates. Left empty it means the owning UIMotion's own GameObject
        /// (UIMotion.WithSelfTargets); a GameObject or any component on it is resolved to the component
        /// the `kind` needs, so the target must actually have one (Fade: a CanvasGroup, or a Graphic such as
        /// Image / TextMeshPro; Move: a RectTransform; ...).</summary>
        [SerializeField] public UnityEngine.Object target;

        /// <summary>Only meaningful when kind == Rect: which RectTransform property this
        /// track animates.</summary>
        public UIMotionRectProperty rectProperty = UIMotionRectProperty.AnchoredPosition;

        /// <summary>Only meaningful when kind == Rect and rectProperty is Pivot or one of
        /// the anchor properties: auto-compensates anchoredPosition/offsets so the rect's
        /// on-screen position doesn't jump when the pivot/anchors move (REWRITE_PLAN.md
        /// 2.7, "Giữ nguyên vị trí hiển thị"). Off reproduces the old MoveAnchorAnimation
        /// behavior of the object visibly shifting.</summary>
        public bool preserveVisualPosition = true;

        public UIMotionStartMode startMode = UIMotionStartMode.WithPrevious;
        public float offset;
        public float duration = 0.25f;

        public bool useCurve;
        public UIEaseType ease = UIEaseType.OutQuad;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public bool useStartValue;
        public UIMotionValueMode fromValueMode = UIMotionValueMode.Absolute;
        public Vector4 from;
        public UIMotionValueMode toValueMode = UIMotionValueMode.Absolute;
        public Vector4 to;

        public bool stagger;
        public float staggerDelay = 0.05f;

        /// <summary>Only meaningful when kind == AnimatorState. `target` is the Animator
        /// itself (same "target" convention as every other kind). `animatorStateName` is
        /// display-only; runtime plays/polls by `animatorStateHash` (REWRITE_PLAN.md 2.7:
        /// "runtime không dùng string"). `animatorStateHash` and the shared `duration`
        /// field (playing the role of the plan's "bakedDuration" here) are meant to be
        /// baked by Editor tooling (OnValidate / AssetPostprocessor / a build preprocessor)
        /// that doesn't exist yet - until then, whoever authors the track computes
        /// `Animator.StringToHash(stateName)` and the clip length itself. `duration` only
        /// schedules AfterPrevious chains for this kind; actual completion is always
        /// polled live (see UIMotionRunner.TickAnimatorTrack), never assumed from it.</summary>
        public int animatorLayer;

        public string animatorStateName = "";
        public int animatorStateHash;
        public float animatorSpeed = 1f;

        /// <summary>Resolved-once component cache for `target`, populated by
        /// UIMotionTrackEvaluator.ResolveTargetCache (called from UIMotion.PrepareTimelines
        /// for every kind of track - authored, stagger-cloned, or built by test code via
        /// ConfigureForTest). Not serialized: rebuilt fresh every time PrepareTimelines runs, so a
        /// stale reference here can never survive a domain reload or an edited `target`.
        /// UIMotionTrackEvaluator.Write reads these directly instead of re-casting/
        /// TryGetComponent-ing `target` every tick - see REWRITE_PLAN.md 2.7 perf notes.</summary>
        [NonSerialized] internal CanvasGroup ResolvedCanvasGroup;

        [NonSerialized] internal RectTransform ResolvedRectTransform;
        [NonSerialized] internal Transform ResolvedTransform;
        [NonSerialized] internal Graphic ResolvedGraphic;

        /// <summary>Fade only: the Graphic whose alpha Fade drives instead of a CanvasGroup (see
        /// UIMotionTrackEvaluator.ResolveTargetCache); null means Fade uses the CanvasGroup.</summary>
        [NonSerialized] internal Graphic ResolvedFadeGraphic;

        [NonSerialized] internal Image ResolvedImage;
        [NonSerialized] internal GameObject ResolvedGameObject;
        [NonSerialized] internal bool ResolvedCacheValid;

        public float Evaluate(float t) => useCurve ? this.curve.Evaluate(t) : UIEase.Evaluate(this.ease, t);

        /// <summary>Field-for-field copy. Used wherever a derived track needs to start
        /// from an authored one and override a few fields - stagger's per-child clones,
        /// and Mirror Hide's generated timeline - instead of each re-listing every field
        /// (near-identical copies is what earned this method). Deliberately does NOT copy the resolved-target
        /// cache above: a clone always gets a different `target` (a stagger child, a
        /// mirrored track, ...), so inheriting the source's cache would point it at the
        /// wrong component until the next PrepareTimelines call happened to overwrite it.</summary>
        public UIMotionTrack Clone() => new()
        {
            kind = this.kind,
            target = this.target,
            rectProperty = this.rectProperty,
            preserveVisualPosition = this.preserveVisualPosition,
            startMode = this.startMode,
            offset = this.offset,
            duration = this.duration,
            useCurve = this.useCurve,
            ease = this.ease,
            curve = this.curve,
            useStartValue = this.useStartValue,
            fromValueMode = this.fromValueMode,
            from = this.from,
            toValueMode = this.toValueMode,
            to = this.to,
            stagger = this.stagger,
            staggerDelay = this.staggerDelay,
            animatorLayer = this.animatorLayer,
            animatorStateName = this.animatorStateName,
            animatorStateHash = this.animatorStateHash,
            animatorSpeed = this.animatorSpeed
        };
    }
}