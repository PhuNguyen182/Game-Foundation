using System;
using DracoRuan.PrebuildServices.UISystem.Editor.MotionTools;
using DracoRuan.PrebuildServices.UISystem.Motion;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Editor.Tests
{
    [TestFixture]
    public sealed class UIMotionTrackFieldVisibilityTests
    {
        private static readonly UIMotionTrackKind[] RangeKinds =
        {
            UIMotionTrackKind.Fade, UIMotionTrackKind.Move, UIMotionTrackKind.Rect, UIMotionTrackKind.Scale,
            UIMotionTrackKind.Rotate, UIMotionTrackKind.Color, UIMotionTrackKind.Fill,
        };

        private static UIMotionTrackKind[] AllKinds => (UIMotionTrackKind[])Enum.GetValues(typeof(UIMotionTrackKind));

        [Test]
        public void RangeKinds_ShowStartTargetAndEaseAndDuration()
        {
            foreach (UIMotionTrackKind kind in RangeKinds)
            {
                Assert.That(UIMotionTrackFieldVisibility.IsRange(kind), Is.True, kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ShowUseStartValue(kind), Is.True, kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ShowEase(kind), Is.True, kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ShowDuration(kind), Is.True, kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ShowToValue(kind), Is.True, kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ShowValueModes(kind), Is.True, kind.ToString());
            }
        }

        [Test]
        public void FromValue_OnlyShowsWhenUseStartValueIsOn()
        {
            Assert.That(UIMotionTrackFieldVisibility.ShowFromValue(UIMotionTrackKind.Fade, useStartValue: false),
                Is.False);
            Assert.That(UIMotionTrackFieldVisibility.ShowFromValue(UIMotionTrackKind.Fade, useStartValue: true),
                Is.True);
            Assert.That(UIMotionTrackFieldVisibility.ShowFromValue(UIMotionTrackKind.SetActive, useStartValue: true),
                Is.False);
        }

        [Test]
        public void SetActive_HasNoDurationOrEaseOrStart_AndUsesAToggle()
        {
            const UIMotionTrackKind kind = UIMotionTrackKind.SetActive;

            Assert.That(UIMotionTrackFieldVisibility.ShowDuration(kind), Is.False);
            Assert.That(UIMotionTrackFieldVisibility.ShowEase(kind), Is.False);
            Assert.That(UIMotionTrackFieldVisibility.ShowUseStartValue(kind), Is.False);
            Assert.That(UIMotionTrackFieldVisibility.ShowValueModes(kind), Is.False);
            Assert.That(UIMotionTrackFieldVisibility.ToShape(kind, UIMotionRectProperty.AnchoredPosition),
                Is.EqualTo(UIMotionValueShape.Toggle));
        }

        [Test]
        public void PunchAndShake_OnlyHaveAnAmplitude_AndNoEase()
        {
            foreach (UIMotionTrackKind kind in new[] { UIMotionTrackKind.Punch, UIMotionTrackKind.Shake })
            {
                Assert.That(UIMotionTrackFieldVisibility.ShowEase(kind), Is.False, kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ShowUseStartValue(kind), Is.False, kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ShowValueModes(kind), Is.False, kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ShowDuration(kind), Is.True, kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ToShape(kind, UIMotionRectProperty.AnchoredPosition),
                    Is.EqualTo(UIMotionValueShape.Amplitude), kind.ToString());
                Assert.That(UIMotionTrackFieldVisibility.ToLabel(kind), Is.EqualTo("Amplitude"), kind.ToString());
            }
        }

        [Test]
        public void Custom_ShowsEaseButNoValuesOrStagger()
        {
            const UIMotionTrackKind kind = UIMotionTrackKind.Custom;

            Assert.That(UIMotionTrackFieldVisibility.ShowEase(kind), Is.True);
            Assert.That(UIMotionTrackFieldVisibility.ShowStagger(kind), Is.False);
            Assert.That(UIMotionTrackFieldVisibility.ShowToValue(kind), Is.False);
            Assert.That(UIMotionTrackFieldVisibility.ShowUseStartValue(kind), Is.False);
            Assert.That(UIMotionTrackFieldVisibility.TargetLabel(kind), Is.EqualTo("Custom Track"));
        }

        [Test]
        public void AnimatorState_OnlyShowsAnimatorFields()
        {
            foreach (UIMotionTrackKind kind in AllKinds)
            {
                bool expected = kind == UIMotionTrackKind.AnimatorState;
                Assert.That(UIMotionTrackFieldVisibility.ShowAnimatorFields(kind), Is.EqualTo(expected),
                    kind.ToString());
            }

            Assert.That(UIMotionTrackFieldVisibility.ShowEase(UIMotionTrackKind.AnimatorState), Is.False);
            Assert.That(
                UIMotionTrackFieldVisibility.ToShape(UIMotionTrackKind.AnimatorState,
                    UIMotionRectProperty.AnchoredPosition), Is.EqualTo(UIMotionValueShape.None));
            Assert.That(UIMotionTrackFieldVisibility.TargetLabel(UIMotionTrackKind.AnimatorState),
                Is.EqualTo("Animator"));
        }

        [Test]
        public void RectProperty_OnlyForRect_AndPreserveOnlyForPivotAndAnchors()
        {
            foreach (UIMotionTrackKind kind in AllKinds)
                Assert.That(UIMotionTrackFieldVisibility.ShowRectProperty(kind),
                    Is.EqualTo(kind == UIMotionTrackKind.Rect), kind.ToString());

            const UIMotionTrackKind rect = UIMotionTrackKind.Rect;
            Assert.That(UIMotionTrackFieldVisibility.ShowPreserveVisualPosition(rect, UIMotionRectProperty.Pivot),
                Is.True);
            Assert.That(UIMotionTrackFieldVisibility.ShowPreserveVisualPosition(rect, UIMotionRectProperty.AnchorMin),
                Is.True);
            Assert.That(UIMotionTrackFieldVisibility.ShowPreserveVisualPosition(rect, UIMotionRectProperty.AnchorMax),
                Is.True);
            Assert.That(UIMotionTrackFieldVisibility.ShowPreserveVisualPosition(rect, UIMotionRectProperty.Anchors),
                Is.True);
            Assert.That(
                UIMotionTrackFieldVisibility.ShowPreserveVisualPosition(rect, UIMotionRectProperty.AnchoredPosition),
                Is.False);
            Assert.That(UIMotionTrackFieldVisibility.ShowPreserveVisualPosition(rect, UIMotionRectProperty.SizeDelta),
                Is.False);
            Assert.That(
                UIMotionTrackFieldVisibility.ShowPreserveVisualPosition(UIMotionTrackKind.Move,
                    UIMotionRectProperty.Pivot), Is.False);
        }

        [Test]
        public void RectValueShape_FollowsTheProperty()
        {
            const UIMotionTrackKind rect = UIMotionTrackKind.Rect;

            Assert.That(UIMotionTrackFieldVisibility.ToShape(rect, UIMotionRectProperty.Anchors),
                Is.EqualTo(UIMotionValueShape.Vector4));
            Assert.That(UIMotionTrackFieldVisibility.ToShape(rect, UIMotionRectProperty.Pivot),
                Is.EqualTo(UIMotionValueShape.Vector2));
            Assert.That(UIMotionTrackFieldVisibility.ToShape(rect, UIMotionRectProperty.SizeDelta),
                Is.EqualTo(UIMotionValueShape.Vector2));
        }

        [Test]
        public void ValueShape_MatchesWhatEachKindReads()
        {
            const UIMotionRectProperty any = UIMotionRectProperty.AnchoredPosition;

            Assert.That(UIMotionTrackFieldVisibility.ToShape(UIMotionTrackKind.Fade, any),
                Is.EqualTo(UIMotionValueShape.Float));
            Assert.That(UIMotionTrackFieldVisibility.ToShape(UIMotionTrackKind.Fill, any),
                Is.EqualTo(UIMotionValueShape.Float));
            Assert.That(UIMotionTrackFieldVisibility.ToShape(UIMotionTrackKind.Move, any),
                Is.EqualTo(UIMotionValueShape.Vector2));
            Assert.That(UIMotionTrackFieldVisibility.ToShape(UIMotionTrackKind.Scale, any),
                Is.EqualTo(UIMotionValueShape.Vector3));
            Assert.That(UIMotionTrackFieldVisibility.ToShape(UIMotionTrackKind.Rotate, any),
                Is.EqualTo(UIMotionValueShape.Vector3));
            Assert.That(UIMotionTrackFieldVisibility.ToShape(UIMotionTrackKind.Color, any),
                Is.EqualTo(UIMotionValueShape.Color));
        }

        [Test]
        public void FromShape_IsOnlyDefinedForRangeKinds()
        {
            foreach (UIMotionTrackKind kind in AllKinds)
            {
                bool range = UIMotionTrackFieldVisibility.IsRange(kind);
                UIMotionValueShape from =
                    UIMotionTrackFieldVisibility.FromShape(kind, UIMotionRectProperty.AnchoredPosition);
                Assert.That(from != UIMotionValueShape.None, Is.EqualTo(range), kind.ToString());
            }
        }

        private static string[] RequiredNames(UIMotionTrackKind kind) =>
            System.Array.ConvertAll(UIMotionTrackFieldVisibility.RequiredComponents(kind), t => t.Name);

        [Test]
        public void RequiredComponents_IsWhatEachKindWritesTo()
        {
            Assert.That(RequiredNames(UIMotionTrackKind.Fade), Is.EquivalentTo(new[] { "CanvasGroup", "Graphic" }));
            Assert.That(RequiredNames(UIMotionTrackKind.Move), Is.EqualTo(new[] { "RectTransform" }));
            Assert.That(RequiredNames(UIMotionTrackKind.Shake), Is.EqualTo(new[] { "RectTransform" }));
            Assert.That(RequiredNames(UIMotionTrackKind.Rect), Is.EqualTo(new[] { "RectTransform" }));
            Assert.That(RequiredNames(UIMotionTrackKind.Scale), Is.EqualTo(new[] { "Transform" }));
            Assert.That(RequiredNames(UIMotionTrackKind.Punch), Is.EqualTo(new[] { "Transform" }));
            Assert.That(RequiredNames(UIMotionTrackKind.Rotate), Is.EqualTo(new[] { "Transform" }));
            Assert.That(RequiredNames(UIMotionTrackKind.Color), Is.EqualTo(new[] { "Graphic" }));
            Assert.That(RequiredNames(UIMotionTrackKind.Fill), Is.EqualTo(new[] { "Image" }));
            Assert.That(RequiredNames(UIMotionTrackKind.AnimatorState), Is.EqualTo(new[] { "Animator" }));
            Assert.That(RequiredNames(UIMotionTrackKind.SetActive), Is.EqualTo(new[] { "GameObject" }));
            Assert.That(RequiredNames(UIMotionTrackKind.Custom), Is.Empty);
        }

        [Test]
        public void FractionOfParent_OnlyForPositionAndSizeLikeValues()
        {
            Assert.That(
                UIMotionTrackFieldVisibility.AllowsFractionOfParent(UIMotionTrackKind.Move,
                    UIMotionRectProperty.AnchoredPosition), Is.True);
            Assert.That(
                UIMotionTrackFieldVisibility.AllowsFractionOfParent(UIMotionTrackKind.Rect,
                    UIMotionRectProperty.AnchoredPosition), Is.True);
            Assert.That(
                UIMotionTrackFieldVisibility.AllowsFractionOfParent(UIMotionTrackKind.Rect,
                    UIMotionRectProperty.SizeDelta), Is.True);
            Assert.That(
                UIMotionTrackFieldVisibility.AllowsFractionOfParent(UIMotionTrackKind.Rect,
                    UIMotionRectProperty.OffsetMin), Is.True);
            Assert.That(
                UIMotionTrackFieldVisibility.AllowsFractionOfParent(UIMotionTrackKind.Rect, UIMotionRectProperty.Pivot),
                Is.False);
            Assert.That(
                UIMotionTrackFieldVisibility.AllowsFractionOfParent(UIMotionTrackKind.Rect,
                    UIMotionRectProperty.Anchors), Is.False);
            Assert.That(
                UIMotionTrackFieldVisibility.AllowsFractionOfParent(UIMotionTrackKind.Fade,
                    UIMotionRectProperty.AnchoredPosition), Is.False);
            Assert.That(
                UIMotionTrackFieldVisibility.AllowsFractionOfParent(UIMotionTrackKind.Scale,
                    UIMotionRectProperty.AnchoredPosition), Is.False);
        }

        [Test]
        public void AllowedModes_RelativeToStartIsTargetOnly_AndFractionFollowsTheKind()
        {
            var from = UIMotionTrackFieldVisibility.AllowedModes(UIMotionTrackKind.Move,
                UIMotionRectProperty.AnchoredPosition, forTarget: false);
            var to = UIMotionTrackFieldVisibility.AllowedModes(UIMotionTrackKind.Move,
                UIMotionRectProperty.AnchoredPosition, forTarget: true);
            var fadeTo = UIMotionTrackFieldVisibility.AllowedModes(UIMotionTrackKind.Fade,
                UIMotionRectProperty.AnchoredPosition, forTarget: true);

            Assert.That(from,
                Has.No.Member(DracoRuan.PrebuildServices.UISystem.Motion.Logic.UIMotionValueMode.RelativeToStart));
            Assert.That(from,
                Has.Member(DracoRuan.PrebuildServices.UISystem.Motion.Logic.UIMotionValueMode.FractionOfParent));
            Assert.That(to,
                Has.Member(DracoRuan.PrebuildServices.UISystem.Motion.Logic.UIMotionValueMode.RelativeToStart));
            Assert.That(fadeTo,
                Has.No.Member(DracoRuan.PrebuildServices.UISystem.Motion.Logic.UIMotionValueMode.FractionOfParent));
            Assert.That(from[0],
                Is.EqualTo(DracoRuan.PrebuildServices.UISystem.Motion.Logic.UIMotionValueMode.Absolute));
        }
    }
}