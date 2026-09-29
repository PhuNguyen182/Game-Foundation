using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    /// <summary>EditMode tests for the Inspector's scrubbable preview (UIMotion.Preview.cs):
    /// the preview must be a pure function of the seek time, restore everything on end, and
    /// never let a bad track escape into the Editor.</summary>
    [TestFixture]
    public sealed class UIMotionPreviewTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            UIMotionTrackEvaluator.PreWrite = null;
            foreach (GameObject go in this._spawned)
                if (go != null)
                    Object.DestroyImmediate(go);
            this._spawned.Clear();
        }

        private UIMotion NewMotion(out GameObject go)
        {
            go = new GameObject("PreviewMotion");
            this._spawned.Add(go);
            return go.AddComponent<UIMotion>();
        }

        private static UIMotionTrack LinearFade(CanvasGroup cg, float from, float to, float duration) => new UIMotionTrack
        {
            kind = UIMotionTrackKind.Fade,
            target = cg,
            useStartValue = true,
            ease = UIEaseType.Linear,
            from = new Vector4(from, 0, 0, 0),
            to = new Vector4(to, 0, 0, 0),
            duration = duration,
        };

        [Test]
        public void Seek_MidWay_GivesTheEasedValueAtThatTime()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            motion.ConfigureForTest(new[] { LinearFade(cg, 0f, 1f, 2f) }, new UIMotionTrack[0]);

            motion.PreviewBegin(show: true);
            motion.PreviewSeek(1f);

            Assert.That(cg.alpha, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(motion.PreviewDuration, Is.EqualTo(2f));
        }

        [Test]
        public void Seek_BackToZero_ReturnsToTheStartValue()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            motion.ConfigureForTest(new[] { LinearFade(cg, 0f, 1f, 2f) }, new UIMotionTrack[0]);

            motion.PreviewBegin(show: true);
            motion.PreviewSeek(2f);
            Assert.That(cg.alpha, Is.EqualTo(1f).Within(0.001f));

            motion.PreviewSeek(0f);

            Assert.That(cg.alpha, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void PreviewEnd_PutsTheTargetBackAtItsRestPose()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0.7f;
            motion.ConfigureForTest(new[] { LinearFade(cg, 0f, 1f, 2f) }, new UIMotionTrack[0]);

            motion.PreviewBegin(show: true);
            motion.PreviewSeek(1f);
            Assert.That(motion.IsPreviewing, Is.True);

            motion.PreviewEnd();

            Assert.That(motion.IsPreviewing, Is.False);
            Assert.That(cg.alpha, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void PreviewBegin_WhileAlreadyPreviewing_DoesNotCaptureTheHalfPlayedPoseAsRest()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0.7f;
            motion.ConfigureForTest(new[] { LinearFade(cg, 0f, 1f, 2f) }, new UIMotionTrack[0]);

            motion.PreviewBegin(show: true);
            motion.PreviewSeek(1f);
            motion.PreviewBegin(show: true);
            motion.PreviewEnd();

            Assert.That(cg.alpha, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void HidePreview_WithMirrorHide_ReversesTheShowTimeline()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            motion.ConfigureForTest(new[] { LinearFade(cg, 0f, 1f, 2f) }, new UIMotionTrack[0], mirrorHide: true);

            motion.PreviewBegin(show: false);
            motion.PreviewSeek(0f);
            Assert.That(cg.alpha, Is.EqualTo(1f).Within(0.001f));

            motion.PreviewSeek(motion.PreviewDuration);

            Assert.That(cg.alpha, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void AnimatorStateTracks_AreSkippedAndCounted_WithoutFailing()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            Animator animator = go.AddComponent<Animator>();
            var animatorTrack = new UIMotionTrack { kind = UIMotionTrackKind.AnimatorState, target = animator, duration = 1f };
            motion.ConfigureForTest(new[] { LinearFade(cg, 0f, 1f, 2f), animatorTrack }, new UIMotionTrack[0]);

            motion.PreviewBegin(show: true);
            motion.PreviewSeek(1f);

            Assert.That(motion.PreviewSkippedTrackCount, Is.EqualTo(1));
            Assert.That(motion.PreviewFailed, Is.False);
            Assert.That(cg.alpha, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void PreviewSpeed_FollowsSpeedOverride()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            motion.ConfigureForTest(new[] { LinearFade(cg, 0f, 1f, 2f) }, new UIMotionTrack[0], speedOverride: 2f);

            Assert.That(motion.PreviewSpeed, Is.EqualTo(2f));
        }

        [Test]
        public void PreWrite_FiresForEveryWrite_AndIsNullByDefault()
        {
            Assert.That(UIMotionTrackEvaluator.PreWrite, Is.Null);

            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            motion.ConfigureForTest(new[] { LinearFade(cg, 0f, 1f, 2f) }, new UIMotionTrack[0]);

            int writes = 0;
            UIMotionTrackEvaluator.PreWrite = _ => writes++;

            motion.PreviewBegin(show: true);
            motion.PreviewSeek(1f);

            Assert.That(writes, Is.GreaterThan(0));
        }

        [Test]
        public void ThrowingCustomTrack_MarksThePreviewFailed_InsteadOfEscaping()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            ThrowingCustomTrack custom = go.AddComponent<ThrowingCustomTrack>();
            var track = new UIMotionTrack { kind = UIMotionTrackKind.Custom, target = custom, duration = 1f };
            motion.ConfigureForTest(new[] { track }, new UIMotionTrack[0]);

            motion.PreviewBegin(show: true);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: preview boom");
            motion.PreviewSeek(0.5f);

            Assert.That(motion.PreviewFailed, Is.True);

            // Once failed, further seeks must not tick (and re-log) again.
            Assert.DoesNotThrow(() => motion.PreviewSeek(0.7f));
        }

        private sealed class ThrowingCustomTrack : MonoBehaviour, IUIMotionCustomTrack
        {
            public void CaptureStart() { }
            public void Sample(float t) => throw new System.InvalidOperationException("preview boom");
            public void Snap(bool toEnd) { }
        }
    }
}
