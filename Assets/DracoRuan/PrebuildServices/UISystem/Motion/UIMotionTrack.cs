using System;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using UnityEngine;

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
        public UIMotionTrackKind kind = UIMotionTrackKind.Fade;
        public UnityEngine.Object target;

        /// <summary>Alternative to a direct `target` reference: null means "use `target`
        /// as-is, ignore this field" (every authored track so far); "" means "resolve to
        /// the applying UIMotion's own Transform (Self)"; a non-empty string is a
        /// `Transform.Find` path relative to it. This is what lets a UIMotionPreset asset
        /// - which cannot hold a scene reference - describe "Self" or "a named child" and
        /// have it resolved fresh against whichever GameObject the preset gets applied to
        /// (REWRITE_PLAN.md 2.7: "Track trong preset trỏ tới Self hoặc tới đường dẫn con").
        /// Resolved once in UIMotion.PrepareTimelines, not re-resolved per tick.</summary>
        public string targetPath;

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

        public int loops = 1;
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

        public float Evaluate(float t) => useCurve ? this.curve.Evaluate(t) : UIEase.Evaluate(this.ease, t);

        /// <summary>Field-for-field copy. Used wherever a derived track needs to start
        /// from an authored one and override a few fields - stagger's per-child clones,
        /// Mirror Hide's generated timeline, and preset target resolution - instead of
        /// each re-listing every field (three near-identical copies is what earned this
        /// method instead of a fourth).</summary>
        public UIMotionTrack Clone() => new UIMotionTrack
        {
            kind = this.kind,
            target = this.target,
            targetPath = this.targetPath,
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
            loops = this.loops,
            stagger = this.stagger,
            staggerDelay = this.staggerDelay,
            animatorLayer = this.animatorLayer,
            animatorStateName = this.animatorStateName,
            animatorStateHash = this.animatorStateHash,
            animatorSpeed = this.animatorSpeed,
        };
    }
}
