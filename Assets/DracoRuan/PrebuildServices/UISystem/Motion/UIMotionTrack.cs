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

        public float Evaluate(float t) => useCurve ? this.curve.Evaluate(t) : UIEase.Evaluate(this.ease, t);
    }
}
