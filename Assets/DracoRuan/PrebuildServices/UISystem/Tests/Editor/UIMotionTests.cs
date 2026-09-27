using System.Collections.Generic;
using System.Threading;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using NUnit.Framework;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    /// <summary>
    /// Real-Editor EditMode tests for UIMotion/UIMotionRunner (see PROGRESS.md - this
    /// asmdef can only run against a live Unity Editor, not the offline compile-check
    /// harness). Every scenario uses duration = 0 tracks or an already-cancelled token,
    /// so the whole playback completes synchronously inside the call that starts it -
    /// no dependency on UIMotionRunner's PlayerLoop tick, which only actually runs in
    /// Play Mode.
    /// </summary>
    [TestFixture]
    public sealed class UIMotionTests
    {
        private const string TempControllerPath = "Assets/_UIMotionTests_TempAnimator.controller";

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in this._spawned)
                if (go != null)
                    Object.DestroyImmediate(go);
            this._spawned.Clear();
            UIMotionRunner.ReduceMotion = false;

            if (UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(TempControllerPath) != null)
                UnityEditor.AssetDatabase.DeleteAsset(TempControllerPath);
        }

        /// <summary>Bakes a throwaway one-state AnimatorController + linear-position clip,
        /// exactly what the plan's (not-yet-built) Editor tooling would eventually bake
        /// onto a track automatically. `stateName`'s hash is what a track's
        /// animatorStateHash should be set to.</summary>
        private Animator NewAnimatorWithState(GameObject go, string stateName, float clipLength)
        {
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(TempControllerPath);
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, clipLength, 1f));
            UnityEditor.AssetDatabase.AddObjectToAsset(clip, controller);
            UnityEditor.Animations.AnimatorState state = controller.layers[0].stateMachine.AddState(stateName);
            state.motion = clip;

            Animator animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            return animator;
        }

        private GameObject NewGameObject(string name)
        {
            var go = new GameObject(name);
            this._spawned.Add(go);
            return go;
        }

        private UIMotion NewMotion(out GameObject go)
        {
            go = this.NewGameObject("Motion");
            return go.AddComponent<UIMotion>();
        }

        [Test]
        public void PlayShowAsync_TokenAlreadyCancelled_SnapsToEndAndCompletes()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                target = cg,
                useStartValue = true,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(1f, 0, 0, 0),
                duration = 10f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            UIMotionPlaybackResult result = motion.PlayShowAsync(cts.Token).GetAwaiter().GetResult();

            Assert.That(result, Is.EqualTo(UIMotionPlaybackResult.Completed));
            Assert.That(cg.alpha, Is.EqualTo(1f));
        }

        [Test]
        public void ReduceMotion_NonZeroDuration_CompletesInstantlyAtEndValue()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                target = cg,
                useStartValue = true,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(1f, 0, 0, 0),
                duration = 10f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            UIMotionRunner.ReduceMotion = true;
            UIMotionPlaybackResult result = motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(result, Is.EqualTo(UIMotionPlaybackResult.Completed));
            Assert.That(cg.alpha, Is.EqualTo(1f));
        }

        [Test]
        public void HideAndDeactivateAsync_DeactivatesAfterHideCompletes()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            go.AddComponent<CanvasGroup>();

            motion.ConfigureForTest(new UIMotionTrack[0], new UIMotionTrack[0]);

            Assert.That(go.activeSelf, Is.True);
            motion.HideAndDeactivateAsync().GetAwaiter().GetResult();
            Assert.That(go.activeSelf, Is.False);
        }

        [Test]
        public void SetActiveAnimated_False_PlaysHideThenDeactivates()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            go.AddComponent<CanvasGroup>();
            motion.ConfigureForTest(new UIMotionTrack[0], new UIMotionTrack[0]);

            motion.SetActiveAnimated(false);

            Assert.That(go.activeSelf, Is.False);
        }

        [Test]
        public void SetActiveAnimated_True_ActivatesImmediately()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            go.AddComponent<CanvasGroup>();
            motion.ConfigureForTest(new UIMotionTrack[0], new UIMotionTrack[0]);
            go.SetActive(false);

            motion.SetActiveAnimated(true);

            Assert.That(go.activeSelf, Is.True);
        }

        [Test]
        public void MirrorHide_UseStartValueTrack_ReversesFromAndTo()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f; // rest pose captured at ConfigureForTest/Awake time below

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                target = cg,
                useStartValue = true,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(1f, 0, 0, 0),
                duration = 0f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0], mirrorHide: true);

            // Mirror Hide does not depend on Show ever having played for a
            // useStartValue track - call Hide directly on a fresh motion.
            UIMotionPlaybackResult result = motion.PlayHideAsync().GetAwaiter().GetResult();

            Assert.That(result, Is.EqualTo(UIMotionPlaybackResult.Completed));
            Assert.That(cg.alpha, Is.EqualTo(0f));
        }

        [Test]
        public void MirrorHide_NonUseStartValueTrack_ShowPlayed_EndsAtRuntimeSnapshot()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero; // rest pose

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Move,
                target = rt,
                useStartValue = false,
                toValueMode = UIMotionValueMode.Absolute,
                to = new Vector4(100f, 0f, 0f, 0f),
                duration = 0f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0], mirrorHide: true);

            // Simulate the object already being displaced when Show starts, so the
            // runtime snapshot (50,0) differs from both rest (0,0) and Show's target (100,0).
            rt.anchoredPosition = new Vector2(50f, 0f);
            motion.PlayShowAsync().GetAwaiter().GetResult();
            Assert.That(rt.anchoredPosition, Is.EqualTo(new Vector2(100f, 0f)));

            motion.PlayHideAsync().GetAwaiter().GetResult();

            Assert.That(rt.anchoredPosition, Is.EqualTo(new Vector2(50f, 0f)));
        }

        [Test]
        public void MirrorHide_NonUseStartValueTrack_ShowNeverPlayed_FallsBackToRest()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero; // rest pose

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Move,
                target = rt,
                useStartValue = false,
                toValueMode = UIMotionValueMode.Absolute,
                to = new Vector4(100f, 0f, 0f, 0f),
                duration = 0f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0], mirrorHide: true);

            rt.anchoredPosition = new Vector2(30f, 0f);
            motion.PlayHideAsync().GetAwaiter().GetResult();

            Assert.That(rt.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Stagger_ExpandsIntoOnePerChild_WithIncreasingAtTimeOffsets()
        {
            UIMotion motion = this.NewMotion(out GameObject parentGo);
            Transform parent = parentGo.transform;
            var children = new GameObject[3];
            for (int i = 0; i < children.Length; i++)
            {
                children[i] = this.NewGameObject($"Child{i}");
                children[i].transform.SetParent(parent);
            }

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                target = parent,
                stagger = true,
                staggerDelay = 0.1f,
                duration = 0.25f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            IReadOnlyList<UIMotionTrack> expanded = motion.ShowExpandedForTest;

            Assert.That(expanded.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(expanded[i].target, Is.EqualTo(children[i]));
                Assert.That(expanded[i].startMode, Is.EqualTo(UIMotionStartMode.AtTime));
                Assert.That(expanded[i].offset, Is.EqualTo(i * 0.1f).Within(1e-5f));
                Assert.That(expanded[i].duration, Is.EqualTo(0.25f));
            }
        }

        [Test]
        public void Stagger_TargetWithNoChildren_ExpandsToEmpty()
        {
            UIMotion motion = this.NewMotion(out GameObject go);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                target = go.transform,
                stagger = true,
                staggerDelay = 0.1f,
                duration = 0.25f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            Assert.That(motion.ShowExpandedForTest.Count, Is.EqualTo(0));
            Assert.DoesNotThrowAsync(async () => await motion.PlayShowAsync());
        }

        [Test]
        public void Stagger_EachChildCapturesItsOwnCurrentValue_NotASharedOne()
        {
            UIMotion motion = this.NewMotion(out GameObject parentGo);
            Transform parent = parentGo.transform;

            GameObject child0 = this.NewGameObject("Child0");
            child0.transform.SetParent(parent);
            CanvasGroup cg0 = child0.AddComponent<CanvasGroup>();
            cg0.alpha = 0.2f;

            GameObject child1 = this.NewGameObject("Child1");
            child1.transform.SetParent(parent);
            CanvasGroup cg1 = child1.AddComponent<CanvasGroup>();
            cg1.alpha = 0.8f;

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                target = parent,
                stagger = true,
                staggerDelay = 0f,
                useStartValue = false,
                toValueMode = UIMotionValueMode.RelativeToStart,
                to = new Vector4(0.1f, 0f, 0f, 0f),
                duration = 0f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(cg0.alpha, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(cg1.alpha, Is.EqualTo(0.9f).Within(1e-5f));
        }

        [Test]
        public void EvaluateOscillation_Punch_AtZeroAndOne_ReturnsExactlyBasePose()
        {
            var basePose = new Vector4(2f, 2f, 2f, 0f);

            Assert.That(UIMotionTrackEvaluator.EvaluateOscillation(UIMotionTrackKind.Punch, basePose, 0.5f, 0f), Is.EqualTo(basePose));
            Assert.That(UIMotionTrackEvaluator.EvaluateOscillation(UIMotionTrackKind.Punch, basePose, 0.5f, 1f), Is.EqualTo(basePose));
        }

        [Test]
        public void EvaluateOscillation_Punch_AtMidpoint_DeviatesFromBasePose()
        {
            var basePose = new Vector4(2f, 2f, 2f, 0f);

            Vector4 mid = UIMotionTrackEvaluator.EvaluateOscillation(UIMotionTrackKind.Punch, basePose, 0.5f, 0.05f);

            Assert.That(mid, Is.Not.EqualTo(basePose));
        }

        [Test]
        public void EvaluateOscillation_Shake_AtZeroAndOne_ReturnsExactlyBasePose()
        {
            var basePose = new Vector4(10f, 5f, 0f, 0f);

            Assert.That(UIMotionTrackEvaluator.EvaluateOscillation(UIMotionTrackKind.Shake, basePose, 3f, 0f), Is.EqualTo(basePose));
            Assert.That(UIMotionTrackEvaluator.EvaluateOscillation(UIMotionTrackKind.Shake, basePose, 3f, 1f), Is.EqualTo(basePose));
        }

        [Test]
        public void Punch_PlayedToCompletion_EndsExactlyAtBaseScale()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            go.transform.localScale = new Vector3(2f, 2f, 2f);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Punch,
                target = go.transform,
                to = new Vector4(0.5f, 0f, 0f, 0f), // amplitude, per EvaluateOscillation
                duration = 0f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(go.transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
        }

        [Test]
        public void Shake_PlayedToCompletion_EndsExactlyAtBasePosition()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(10f, 5f);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Shake,
                target = rt,
                to = new Vector4(8f, 0f, 0f, 0f), // amplitude, per EvaluateOscillation
                duration = 0f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(rt.anchoredPosition, Is.EqualTo(new Vector2(10f, 5f)));
        }

        [Test]
        public void Custom_PlayedToCompletion_CallsCaptureStartThenSamplesUpToOne()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            FakeCustomTrack fake = go.AddComponent<FakeCustomTrack>();

            var show = new UIMotionTrack { kind = UIMotionTrackKind.Custom, target = fake, duration = 0f };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(fake.CaptureStartCalls, Is.EqualTo(1));
            Assert.That(fake.Samples, Is.Not.Empty);
            Assert.That(fake.Samples[fake.Samples.Count - 1], Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Custom_SnapShow_CallsSnapWithToEndTrue()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            FakeCustomTrack fake = go.AddComponent<FakeCustomTrack>();

            var show = new UIMotionTrack { kind = UIMotionTrackKind.Custom, target = fake, duration = 0f };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.SnapShow();

            Assert.That(fake.LastSnapToEnd, Is.EqualTo(true));
        }

        [Test]
        public void Custom_Restart_SnapsToStart_BeforePlayingAgain()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            FakeCustomTrack fake = go.AddComponent<FakeCustomTrack>();

            var show = new UIMotionTrack { kind = UIMotionTrackKind.Custom, target = fake, duration = 0f };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.Restart();

            Assert.That(fake.LastSnapToEnd, Is.EqualTo(false));
            Assert.That(fake.CaptureStartCalls, Is.EqualTo(1));
        }

        private RectTransform NewPointAnchoredRect(string name, Transform parent, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            GameObject go = this.NewGameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPosition;
            return rt;
        }

        private static void AssertCornersEqual(Vector3[] a, Vector3[] b)
        {
            for (int i = 0; i < 4; i++)
                Assert.That(Vector3.Distance(a[i], b[i]), Is.LessThan(0.01f), $"corner {i}: {a[i]} vs {b[i]}");
        }

        private static void AssertCornersDiffer(Vector3[] a, Vector3[] b)
        {
            bool anyDiffer = false;
            for (int i = 0; i < 4; i++)
                if (Vector3.Distance(a[i], b[i]) > 0.01f)
                    anyDiffer = true;
            Assert.That(anyDiffer, Is.True, "expected corners to move, but they did not");
        }

        [Test]
        public void Rect_Anchors_WithCompensationOn_KeepsWorldCornersUnchanged()
        {
            UIMotion motion = this.NewMotion(out GameObject motionGo);
            RectTransform parentRt = NewPointAnchoredRect("Parent", null, new Vector2(200f, 100f), Vector2.zero);
            RectTransform childRt = this.NewPointAnchoredRect("Child", parentRt, new Vector2(50f, 50f), new Vector2(20f, 10f));

            var before = new Vector3[4];
            childRt.GetWorldCorners(before);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Rect,
                rectProperty = UIMotionRectProperty.Anchors,
                target = childRt,
                useStartValue = true,
                fromValueMode = UIMotionValueMode.Absolute,
                from = new Vector4(0f, 0f, 0f, 0f),
                toValueMode = UIMotionValueMode.Absolute,
                to = new Vector4(0.5f, 0f, 0.5f, 0f),
                duration = 0f,
                preserveVisualPosition = true,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            var after = new Vector3[4];
            childRt.GetWorldCorners(after);
            AssertCornersEqual(before, after);
            Assert.That(childRt.anchorMin.x, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Rect_Anchors_WithCompensationOff_MovesTheRect()
        {
            UIMotion motion = this.NewMotion(out GameObject motionGo);
            RectTransform parentRt = NewPointAnchoredRect("Parent", null, new Vector2(200f, 100f), Vector2.zero);
            RectTransform childRt = this.NewPointAnchoredRect("Child", parentRt, new Vector2(50f, 50f), new Vector2(20f, 10f));

            var before = new Vector3[4];
            childRt.GetWorldCorners(before);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Rect,
                rectProperty = UIMotionRectProperty.Anchors,
                target = childRt,
                useStartValue = true,
                fromValueMode = UIMotionValueMode.Absolute,
                from = new Vector4(0f, 0f, 0f, 0f),
                toValueMode = UIMotionValueMode.Absolute,
                to = new Vector4(0.5f, 0f, 0.5f, 0f),
                duration = 0f,
                preserveVisualPosition = false,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            var after = new Vector3[4];
            childRt.GetWorldCorners(after);
            AssertCornersDiffer(before, after);
        }

        [Test]
        public void Rect_Pivot_WithCompensationOn_KeepsWorldCornersUnchanged()
        {
            UIMotion motion = this.NewMotion(out GameObject motionGo);
            RectTransform parentRt = NewPointAnchoredRect("Parent", null, new Vector2(200f, 100f), Vector2.zero);
            RectTransform childRt = this.NewPointAnchoredRect("Child", parentRt, new Vector2(50f, 50f), new Vector2(20f, 10f));
            childRt.pivot = new Vector2(0.5f, 0.5f);

            var before = new Vector3[4];
            childRt.GetWorldCorners(before);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Rect,
                rectProperty = UIMotionRectProperty.Pivot,
                target = childRt,
                useStartValue = true,
                fromValueMode = UIMotionValueMode.Absolute,
                from = new Vector4(0.5f, 0.5f, 0f, 0f),
                toValueMode = UIMotionValueMode.Absolute,
                to = new Vector4(0f, 0f, 0f, 0f),
                duration = 0f,
                preserveVisualPosition = true,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            var after = new Vector3[4];
            childRt.GetWorldCorners(after);
            AssertCornersEqual(before, after);
            Assert.That(childRt.pivot, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Rect_Pivot_WithCompensationOff_MovesTheRect()
        {
            UIMotion motion = this.NewMotion(out GameObject motionGo);
            RectTransform parentRt = NewPointAnchoredRect("Parent", null, new Vector2(200f, 100f), Vector2.zero);
            RectTransform childRt = this.NewPointAnchoredRect("Child", parentRt, new Vector2(50f, 50f), new Vector2(20f, 10f));
            childRt.pivot = new Vector2(0.5f, 0.5f);

            var before = new Vector3[4];
            childRt.GetWorldCorners(before);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Rect,
                rectProperty = UIMotionRectProperty.Pivot,
                target = childRt,
                useStartValue = true,
                fromValueMode = UIMotionValueMode.Absolute,
                from = new Vector4(0.5f, 0.5f, 0f, 0f),
                toValueMode = UIMotionValueMode.Absolute,
                to = new Vector4(0f, 0f, 0f, 0f),
                duration = 0f,
                preserveVisualPosition = false,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            var after = new Vector3[4];
            childRt.GetWorldCorners(after);
            AssertCornersDiffer(before, after);
        }

        [Test]
        public void Rect_AnchoredPositionAndSizeDelta_CaptureAndWrite_RoundTrip()
        {
            UIMotion motion = this.NewMotion(out GameObject motionGo);
            RectTransform parentRt = NewPointAnchoredRect("Parent", null, new Vector2(200f, 100f), Vector2.zero);
            RectTransform childRt = this.NewPointAnchoredRect("Child", parentRt, new Vector2(50f, 50f), new Vector2(0f, 0f));

            var showPos = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Rect,
                rectProperty = UIMotionRectProperty.AnchoredPosition,
                target = childRt,
                useStartValue = true,
                fromValueMode = UIMotionValueMode.Absolute,
                from = new Vector4(0f, 0f, 0f, 0f),
                toValueMode = UIMotionValueMode.Absolute,
                to = new Vector4(15f, 25f, 0f, 0f),
                duration = 0f,
            };
            var showSize = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Rect,
                rectProperty = UIMotionRectProperty.SizeDelta,
                target = childRt,
                useStartValue = true,
                fromValueMode = UIMotionValueMode.Absolute,
                from = new Vector4(50f, 50f, 0f, 0f),
                toValueMode = UIMotionValueMode.Absolute,
                to = new Vector4(80f, 40f, 0f, 0f),
                duration = 0f,
            };
            motion.ConfigureForTest(new[] { showPos, showSize }, new UIMotionTrack[0]);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(childRt.anchoredPosition, Is.EqualTo(new Vector2(15f, 25f)));
            Assert.That(childRt.sizeDelta, Is.EqualTo(new Vector2(80f, 40f)));
        }

        [Test]
        public void AnimatorState_ZeroDuration_JumpsStraightToEndAndDisablesAnimator()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            Animator animator = this.NewAnimatorWithState(go, "TestState", clipLength: 1f);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.AnimatorState,
                target = animator,
                animatorLayer = 0,
                animatorStateHash = Animator.StringToHash("TestState"),
                duration = 0f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            UIMotionPlaybackResult result = motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(result, Is.EqualTo(UIMotionPlaybackResult.Completed));
            Assert.That(animator.enabled, Is.False);
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(info.shortNameHash, Is.EqualTo(show.animatorStateHash));
            Assert.That(info.normalizedTime, Is.GreaterThanOrEqualTo(1f));
        }

        [Test]
        public void AnimatorState_InterruptedMidFlight_DisablesAnimatorInstead_OfLeavingItRunning()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            Animator animator = this.NewAnimatorWithState(go, "TestState", clipLength: 1f);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.AnimatorState,
                target = animator,
                animatorStateHash = Animator.StringToHash("TestState"),
                duration = 10f, // long enough that Show does not finish on its own
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            _ = motion.PlayShowAsync();
            Assert.That(animator.enabled, Is.True, "Show should have started and enabled the Animator");

            // Hide (even with no tracks of its own) stops the in-flight Show via
            // PlayCore -> StopCurrent -> handle.Stop(), which must freeze (disable) any
            // AnimatorState track Show left running - otherwise Unity keeps ticking it
            // forever regardless of this runner's own state.
            motion.PlayHideAsync().GetAwaiter().GetResult();

            Assert.That(animator.enabled, Is.False);
        }

        [Test]
        public void AnimatorState_SnapShow_PlaysToNormalizedTimeOneThenDisables()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            Animator animator = this.NewAnimatorWithState(go, "TestState", clipLength: 1f);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.AnimatorState,
                target = animator,
                animatorStateHash = Animator.StringToHash("TestState"),
                duration = 10f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            motion.SnapShow();

            Assert.That(animator.enabled, Is.False);
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(info.normalizedTime, Is.GreaterThanOrEqualTo(1f));
        }

        [Test]
        public void AnimatorState_Restart_ResetsToNormalizedTimeZeroBeforePlayingAgain()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            Animator animator = this.NewAnimatorWithState(go, "TestState", clipLength: 1f);

            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.AnimatorState,
                target = animator,
                animatorStateHash = Animator.StringToHash("TestState"),
                duration = 10f,
            };
            motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);

            // First bring it to the end, exactly like SnapShow does, then Restart and
            // check the reset-to-start step landed at normalizedTime 0 before Show's own
            // Play(..., 0f) took over again (both leave the Animator enabled afterward,
            // since Restart immediately starts a new Show).
            motion.SnapShow();
            motion.Restart();

            Assert.That(animator.enabled, Is.True, "Restart immediately plays Show again");
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(info.normalizedTime, Is.LessThan(1f));
        }

        [Test]
        public void Preset_SelfPath_ResolvesToTheApplyingMotionsOwnTransform()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                targetPath = "", // Self
                useStartValue = true,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(1f, 0, 0, 0),
                duration = 0f,
            };
            preset.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);
            motion.ConfigureForTest(new UIMotionTrack[0], new UIMotionTrack[0], preset: preset);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(cg.alpha, Is.EqualTo(1f));
            Object.DestroyImmediate(preset);
        }

        [Test]
        public void Preset_ChildPath_ResolvesToTheNamedChildUnderTheApplyingMotion()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            GameObject child = this.NewGameObject("Icon");
            child.transform.SetParent(go.transform);
            CanvasGroup childCg = child.AddComponent<CanvasGroup>();
            childCg.alpha = 0f;

            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                targetPath = "Icon",
                useStartValue = true,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(1f, 0, 0, 0),
                duration = 0f,
            };
            preset.ConfigureForTest(new[] { show }, new UIMotionTrack[0]);
            motion.ConfigureForTest(new UIMotionTrack[0], new UIMotionTrack[0], preset: preset);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(childCg.alpha, Is.EqualTo(1f));
            Object.DestroyImmediate(preset);
        }

        [Test]
        public void Preset_MirrorHideFlag_IsRespected()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            var show = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                targetPath = "",
                useStartValue = true,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(1f, 0, 0, 0),
                duration = 0f,
            };
            preset.ConfigureForTest(new[] { show }, new UIMotionTrack[0], mirrorHide: true);
            motion.ConfigureForTest(new UIMotionTrack[0], new UIMotionTrack[0], preset: preset);

            // Mirror Hide of a useStartValue track ends at its authored Start (0),
            // proving the preset's own mirrorHide=true took effect (the component's own
            // inline `mirrorHide` field, left at its default false, is ignored while a
            // preset is referenced).
            motion.PlayHideAsync().GetAwaiter().GetResult();

            Assert.That(cg.alpha, Is.EqualTo(0f));
            Object.DestroyImmediate(preset);
        }

        [Test]
        public void Preset_WhenSet_InlineTracksAreIgnored()
        {
            UIMotion motion = this.NewMotion(out GameObject go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var inlineShow = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                target = cg,
                useStartValue = true,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(0.3f, 0, 0, 0), // distinct from the preset's target, to tell them apart
                duration = 0f,
            };

            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            var presetShow = new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                targetPath = "",
                useStartValue = true,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(1f, 0, 0, 0),
                duration = 0f,
            };
            preset.ConfigureForTest(new[] { presetShow }, new UIMotionTrack[0]);
            motion.ConfigureForTest(new[] { inlineShow }, new UIMotionTrack[0], preset: preset);

            motion.PlayShowAsync().GetAwaiter().GetResult();

            Assert.That(cg.alpha, Is.EqualTo(1f), "should have played the preset's track, not the inline one");
            Object.DestroyImmediate(preset);
        }

        private sealed class FakeCustomTrack : MonoBehaviour, IUIMotionCustomTrack
        {
            public int CaptureStartCalls;
            public readonly List<float> Samples = new List<float>();
            public bool? LastSnapToEnd;

            public void CaptureStart() => this.CaptureStartCalls++;
            public void Sample(float t) => this.Samples.Add(t);
            public void Snap(bool toEnd) => this.LastSnapToEnd = toEnd;
        }
    }
}
