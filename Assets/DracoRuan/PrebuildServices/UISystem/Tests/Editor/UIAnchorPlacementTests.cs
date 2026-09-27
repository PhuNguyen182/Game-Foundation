using DracoRuan.PrebuildServices.UISystem.Components;
using NUnit.Framework;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class UIAnchorPlacementTests
    {
        private static readonly Rect FullScreen = new Rect(0f, 0f, 1000f, 1000f);

        [Test]
        public void ChooseSide_FirstPriorityFits_PicksIt()
        {
            // Target near the middle of a big screen: every side has room, so the first
            // priority entry (Above) must win outright.
            var target = new Rect(450f, 450f, 100f, 100f);
            var priority = new[] { UIAnchorSide.Above, UIAnchorSide.Below, UIAnchorSide.Right, UIAnchorSide.Left };

            UIAnchorSide chosen = UIAnchorPlacement.ChooseSide(
                priority, target, new Vector2(80f, 40f), spacing: 8f, FullScreen, out Vector2 screenPosition);

            Assert.AreEqual(UIAnchorSide.Above, chosen);
            Assert.Greater(screenPosition.y, target.yMax);
        }

        [Test]
        public void ChooseSide_FirstPriorityDoesNotFit_FallsBackToNext()
        {
            // Target pinned to the very top edge: "Above" would push the tooltip off-screen,
            // so the algorithm must skip it and fall back to "Below".
            var target = new Rect(450f, FullScreen.yMax - 20f, 100f, 20f);
            var priority = new[] { UIAnchorSide.Above, UIAnchorSide.Below, UIAnchorSide.Right, UIAnchorSide.Left };

            UIAnchorSide chosen = UIAnchorPlacement.ChooseSide(
                priority, target, new Vector2(80f, 40f), spacing: 8f, FullScreen, out Vector2 screenPosition);

            Assert.AreEqual(UIAnchorSide.Below, chosen);
            Assert.Less(screenPosition.y, target.yMin);
        }

        [Test]
        public void ChooseSide_NoSideFits_FallsBackToFirstPriority_ClampedInsideBounds()
        {
            // A screen too small for the tooltip on any side: every candidate is rejected by
            // FitsWithin, so the algorithm falls back to the first (highest-priority) entry
            // (Above) and clamps the result inside bounds rather than returning an off-screen
            // point - "prefer the most-wanted side, just keep it on screen" reads better than
            // an arbitrary pick from the least-wanted end of the list.
            var tinyBounds = new Rect(0f, 0f, 120f, 120f);
            var target = new Rect(40f, 40f, 40f, 40f);
            var priority = new[] { UIAnchorSide.Above, UIAnchorSide.Below, UIAnchorSide.Right, UIAnchorSide.Left };

            UIAnchorSide chosen = UIAnchorPlacement.ChooseSide(
                priority, target, new Vector2(80f, 80f), spacing: 8f, tinyBounds, out Vector2 screenPosition);

            Assert.AreEqual(UIAnchorSide.Above, chosen);
            Assert.GreaterOrEqual(screenPosition.x, tinyBounds.xMin + 40f);
            Assert.LessOrEqual(screenPosition.x, tinyBounds.xMax - 40f + 0.01f);
            Assert.GreaterOrEqual(screenPosition.y, tinyBounds.yMin + 40f);
            Assert.LessOrEqual(screenPosition.y, tinyBounds.yMax - 40f + 0.01f);
        }

        [Test]
        public void ChooseSide_RightSide_PositionsToTheRightOfTarget()
        {
            var target = new Rect(100f, 450f, 60f, 60f);
            var priority = new[] { UIAnchorSide.Right };

            UIAnchorPlacement.ChooseSide(
                priority, target, new Vector2(50f, 50f), spacing: 10f, FullScreen, out Vector2 screenPosition);

            Assert.Greater(screenPosition.x, target.xMax);
        }
    }
}
