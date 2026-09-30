using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.Editor.MotionTools;
using DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic.DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UIMotion = DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion.UIMotion;

namespace DracoRuan.PrebuildServices.UISystem.Editor.Tests.DracoRuan.PrebuildServices.UISystem.Editor.Tests
{
    /// <summary>Drives the real Inspector preview controller against the live Editor's
    /// AnimationMode: play/pause/scrub/stop, and the guarantee that stopping AnimationMode alone
    /// (without UIMotion.PreviewEnd running) still puts every animated property back.</summary>
    [TestFixture]
    public sealed class UIMotionPreviewControllerTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            UIMotionPreviewController.Stop();
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            foreach (GameObject go in this._spawned)
                if (go != null)
                    Object.DestroyImmediate(go);
            this._spawned.Clear();
        }

        private UIMotion NewMotion(CanvasGroup[] cgOut, float restAlpha, float duration = 2f)
        {
            var go = new GameObject("PreviewControllerMotion");
            this._spawned.Add(go);
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = restAlpha;
            cgOut[0] = cg;
            var motion = go.AddComponent<UIMotion>();

            SetShowTrack(motion, new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                target = cg,
                useStartValue = true,
                ease = UIEaseType.Linear,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(1f, 0, 0, 0),
                duration = duration,
            });
            return motion;
        }

        private static void SetShowTrack(UIMotion motion, UIMotionTrack track)
        {
            var serialized = new SerializedObject(motion);
            SerializedProperty show = serialized.FindProperty("showTracks");
            show.arraySize = 1;
            show.GetArrayElementAtIndex(0).boxedValue = track;
            serialized.ApplyModifiedProperties();
        }

        [Test]
        public void PlayThenScrub_SamplesTheTimelineAtThatTime_AndOwnsAnimationMode()
        {
            var cg = new CanvasGroup[1];
            UIMotion motion = this.NewMotion(cg, restAlpha: 0.7f);

            UIMotionPreviewController.Play(motion, show: true);

            Assert.That(UIMotionPreviewController.State, Is.EqualTo(UIMotionPreviewState.Playing));
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            Assert.That(UIMotionPreviewController.Duration, Is.EqualTo(2f));

            UIMotionPreviewController.Pause();
            UIMotionPreviewController.Scrub(motion, 1f);
            Assert.That(cg[0].alpha, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(UIMotionPreviewController.State, Is.EqualTo(UIMotionPreviewState.Paused));

            UIMotionPreviewController.Scrub(motion, 0f);
            Assert.That(cg[0].alpha, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void StoppingAnimationModeAlone_RestoresTheAnimatedProperty()
        {
            var cg = new CanvasGroup[1];
            UIMotion motion = this.NewMotion(cg, restAlpha: 0.7f);

            UIMotionPreviewController.Play(motion, show: true);
            UIMotionPreviewController.Pause();
            UIMotionPreviewController.Scrub(motion, 1.5f);
            Assert.That(cg[0].alpha, Is.EqualTo(0.75f).Within(0.001f));

            // Simulates a preview that never got to run PreviewEnd (crash, domain reload...):
            // only Unity's own AnimationMode restore is left to put the value back.
            AnimationMode.StopAnimationMode();

            Assert.That(cg[0].alpha, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void Stop_RestoresTheRestPose_AndReleasesAnimationMode()
        {
            var cg = new CanvasGroup[1];
            UIMotion motion = this.NewMotion(cg, restAlpha: 0.7f);

            UIMotionPreviewController.Play(motion, show: true);
            UIMotionPreviewController.Pause();
            UIMotionPreviewController.Scrub(motion, 1f);

            UIMotionPreviewController.Stop();

            Assert.That(cg[0].alpha, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(UIMotionPreviewController.State, Is.EqualTo(UIMotionPreviewState.Stopped));
            Assert.That(UIMotionPreviewController.Target == null, Is.True);
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            Assert.That(motion.IsPreviewing, Is.False);
        }

        [Test]
        public void Play_WhileAnotherWindowOwnsAnimationMode_IsRefusedWithAMessage()
        {
            var cg = new CanvasGroup[1];
            UIMotion motion = this.NewMotion(cg, restAlpha: 0.7f);

            AnimationMode.StartAnimationMode();
            UIMotionPreviewController.Play(motion, show: true);

            Assert.That(UIMotionPreviewController.State, Is.EqualTo(UIMotionPreviewState.Stopped));
            Assert.That(UIMotionPreviewController.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(motion.IsPreviewing, Is.False);
            Assert.That(cg[0].alpha, Is.EqualTo(0.7f));
        }

        [Test]
        public void NotifyEdited_RebuildsThePreviewInPlace_KeepingTheTime()
        {
            var cg = new CanvasGroup[1];
            UIMotion motion = this.NewMotion(cg, restAlpha: 0.7f, duration: 2f);

            UIMotionPreviewController.Play(motion, show: true);
            UIMotionPreviewController.Pause();
            UIMotionPreviewController.Scrub(motion, 1f);
            Assert.That(cg[0].alpha, Is.EqualTo(0.5f).Within(0.001f));

            // The user drags the duration from 2s to 4s in the Inspector.
            SetShowTrack(motion, new UIMotionTrack
            {
                kind = UIMotionTrackKind.Fade,
                target = cg[0],
                useStartValue = true,
                ease = UIEaseType.Linear,
                from = new Vector4(0f, 0, 0, 0),
                to = new Vector4(1f, 0, 0, 0),
                duration = 4f,
            });
            UIMotionPreviewController.NotifyEdited(motion);

            Assert.That(UIMotionPreviewController.Duration, Is.EqualTo(4f));
            Assert.That(UIMotionPreviewController.Time, Is.EqualTo(1f).Within(0.001f));
            Assert.That(cg[0].alpha, Is.EqualTo(0.25f).Within(0.001f));

            UIMotionPreviewController.Stop();
            Assert.That(cg[0].alpha, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void Resume_FromTheEnd_RestartsFromZero()
        {
            var cg = new CanvasGroup[1];
            UIMotion motion = this.NewMotion(cg, restAlpha: 0.7f);

            UIMotionPreviewController.Play(motion, show: true);
            UIMotionPreviewController.Pause();
            UIMotionPreviewController.Scrub(motion, UIMotionPreviewController.Duration);
            Assert.That(cg[0].alpha, Is.EqualTo(1f).Within(0.001f));

            UIMotionPreviewController.Resume();

            Assert.That(UIMotionPreviewController.State, Is.EqualTo(UIMotionPreviewState.Playing));
            Assert.That(UIMotionPreviewController.Time, Is.EqualTo(0f));
        }

        private GameObject NewRectObject(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            this._spawned.Add(go);
            return go;
        }

        /// <summary>Previews `track` on `go` to 0.4s, checks `read` changed, then stops AnimationMode
        /// alone and checks it came back - i.e. that the recorder registered the right serialized
        /// property paths for this kind.</summary>
        private void AssertAnimationModeRestores(
            string label, GameObject go, UIMotionTrack track, System.Func<Vector4> read)
        {
            Vector4 rest = read();
            var motion = go.AddComponent<UIMotion>();
            SetShowTrack(motion, track);

            UIMotionPreviewController.Play(motion, show: true);
            UIMotionPreviewController.Pause();
            UIMotionPreviewController.Scrub(motion, 0.4f);

            Assert.That((read() - rest).sqrMagnitude, Is.GreaterThan(1e-6f), label + ": preview should change the value");

            AnimationMode.StopAnimationMode();

            Assert.That((read() - rest).sqrMagnitude, Is.LessThan(1e-8f), label + ": AnimationMode alone should restore it");
            UIMotionPreviewController.Stop();
        }

        [Test]
        public void EveryKindsProperties_AreRegisteredWithAnimationMode()
        {
            GameObject move = this.NewRectObject("Move");
            ((RectTransform)move.transform).anchoredPosition = new Vector2(5f, 6f);
            this.AssertAnimationModeRestores("Move", move,
                new UIMotionTrack { kind = UIMotionTrackKind.Move, target = move, ease = UIEaseType.Linear, to = new Vector4(100f, 200f, 0, 0), duration = 1f },
                () => (Vector2)((RectTransform)move.transform).anchoredPosition);

            GameObject shake = this.NewRectObject("Shake");
            ((RectTransform)shake.transform).anchoredPosition = new Vector2(1f, 2f);
            this.AssertAnimationModeRestores("Shake", shake,
                new UIMotionTrack { kind = UIMotionTrackKind.Shake, target = shake, to = new Vector4(30f, 0, 0, 0), duration = 1f },
                () => (Vector2)((RectTransform)shake.transform).anchoredPosition);

            GameObject scale = this.NewRectObject("Scale");
            this.AssertAnimationModeRestores("Scale", scale,
                new UIMotionTrack { kind = UIMotionTrackKind.Scale, target = scale, ease = UIEaseType.Linear, to = new Vector4(2f, 2f, 2f, 0), duration = 1f },
                () => scale.transform.localScale);

            GameObject punch = this.NewRectObject("Punch");
            this.AssertAnimationModeRestores("Punch", punch,
                new UIMotionTrack { kind = UIMotionTrackKind.Punch, target = punch, to = new Vector4(0.5f, 0, 0, 0), duration = 1f },
                () => punch.transform.localScale);

            GameObject rotate = this.NewRectObject("Rotate");
            this.AssertAnimationModeRestores("Rotate", rotate,
                new UIMotionTrack { kind = UIMotionTrackKind.Rotate, target = rotate, ease = UIEaseType.Linear, to = new Vector4(0f, 0f, 90f, 0), duration = 1f },
                () => rotate.transform.localEulerAngles);

            GameObject color = this.NewRectObject("Color");
            var colorImage = color.AddComponent<UnityEngine.UI.Image>();
            colorImage.color = new Color(1f, 0f, 0f, 1f);
            this.AssertAnimationModeRestores("Color", color,
                new UIMotionTrack { kind = UIMotionTrackKind.Color, target = colorImage, ease = UIEaseType.Linear, to = new Vector4(0f, 1f, 0f, 0.5f), duration = 1f },
                () => colorImage.color);

            GameObject fadeImage = this.NewRectObject("FadeImage");
            var fadeImageGraphic = fadeImage.AddComponent<UnityEngine.UI.Image>();
            fadeImageGraphic.color = new Color(1f, 1f, 1f, 1f);
            this.AssertAnimationModeRestores("Fade.Image", fadeImage,
                new UIMotionTrack { kind = UIMotionTrackKind.Fade, target = fadeImage, ease = UIEaseType.Linear, to = new Vector4(0f, 0, 0, 0), duration = 1f },
                () => fadeImageGraphic.color);

            GameObject fadeText = this.NewRectObject("FadeText");
            var fadeTextGraphic = fadeText.AddComponent<TMPro.TextMeshProUGUI>();
            fadeTextGraphic.color = new Color(1f, 1f, 1f, 1f);
            this.AssertAnimationModeRestores("Fade.TextMeshPro", fadeText,
                new UIMotionTrack { kind = UIMotionTrackKind.Fade, target = fadeText, ease = UIEaseType.Linear, to = new Vector4(0f, 0, 0, 0), duration = 1f },
                () => fadeTextGraphic.color);

            GameObject fill = this.NewRectObject("Fill");
            var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
            fillImage.fillAmount = 1f;
            this.AssertAnimationModeRestores("Fill", fill,
                new UIMotionTrack { kind = UIMotionTrackKind.Fill, target = fillImage, ease = UIEaseType.Linear, to = new Vector4(0.2f, 0, 0, 0), duration = 1f },
                () => new Vector4(fillImage.fillAmount, 0, 0, 0));

            GameObject active = this.NewRectObject("SetActive");
            this.AssertAnimationModeRestores("SetActive", active,
                new UIMotionTrack { kind = UIMotionTrackKind.SetActive, target = active, to = new Vector4(0f, 0, 0, 0), duration = 0f },
                () => new Vector4(active.activeSelf ? 1f : 0f, 0, 0, 0));

            GameObject size = this.NewRectObject("Size");
            ((RectTransform)size.transform).sizeDelta = new Vector2(10f, 10f);
            this.AssertAnimationModeRestores("Rect.SizeDelta", size,
                new UIMotionTrack { kind = UIMotionTrackKind.Rect, rectProperty = UIMotionRectProperty.SizeDelta, target = size, ease = UIEaseType.Linear, to = new Vector4(50f, 60f, 0, 0), duration = 1f },
                () => (Vector2)((RectTransform)size.transform).sizeDelta);

            GameObject anchors = this.NewRectObject("Anchors");
            GameObject parent = this.NewRectObject("AnchorsParent");
            ((RectTransform)parent.transform).sizeDelta = new Vector2(200f, 100f);
            anchors.transform.SetParent(parent.transform, false);
            var anchorsRect = (RectTransform)anchors.transform;
            anchorsRect.sizeDelta = new Vector2(20f, 20f);
            this.AssertAnimationModeRestores("Rect.Anchors", anchors,
                new UIMotionTrack { kind = UIMotionTrackKind.Rect, rectProperty = UIMotionRectProperty.Anchors, preserveVisualPosition = true, target = anchors, ease = UIEaseType.Linear, to = new Vector4(0f, 0f, 1f, 1f), duration = 1f },
                () => new Vector4(anchorsRect.anchorMin.x + anchorsRect.anchoredPosition.x, anchorsRect.anchorMax.y + anchorsRect.sizeDelta.y, anchorsRect.offsetMin.x, anchorsRect.offsetMax.y));
        }
    }
}
