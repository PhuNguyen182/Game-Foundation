using DracoRuan.PrebuildServices.UISystem.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class UITutorialMaskControllerTests
    {
        [Test]
        public void ComputeBlockerRects_CenteredHole_ProducesNonOverlappingFullCoverage()
        {
            // Hole centered at (0.5, 0.5) with half-size (0.1, 0.1) => hole spans x:[0.4,0.6], y:[0.4,0.6].
            var hole = new Vector4(0.5f, 0.5f, 0.1f, 0.1f);

            (Vector4 top, Vector4 bottom, Vector4 left, Vector4 right) = UITutorialMaskController.ComputeBlockerRects(hole);

            // Top spans the full width above the hole.
            Assert.AreEqual(new Vector4(0f, 0.6f, 1f, 1f), top);
            // Bottom spans the full width below the hole.
            Assert.AreEqual(new Vector4(0f, 0f, 1f, 0.4f), bottom);
            // Left/Right fill exactly the band between top and bottom, beside the hole.
            Assert.AreEqual(new Vector4(0f, 0.4f, 0.4f, 0.6f), left);
            Assert.AreEqual(new Vector4(0.6f, 0.4f, 1f, 0.6f), right);
        }

        [Test]
        public void ComputeBlockerRects_HoleInCorner_StillProducesConsistentRects()
        {
            var hole = new Vector4(0.1f, 0.9f, 0.1f, 0.1f);

            (Vector4 top, Vector4 bottom, Vector4 left, Vector4 right) = UITutorialMaskController.ComputeBlockerRects(hole);

            Assert.AreEqual(1f, top.w);
            Assert.AreEqual(0f, bottom.y);
            Assert.Less(left.z, 0.2f);
            Assert.Greater(right.x, 0.15f);
        }

        [Test]
        public void ComputeBlockerRects_FullScreenHole_BlockersCollapseToZeroArea()
        {
            var hole = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);

            (Vector4 top, Vector4 bottom, Vector4 left, Vector4 right) = UITutorialMaskController.ComputeBlockerRects(hole);

            // top: yMin == yMax == 1, bottom: yMin == yMax == 0 => both zero-height.
            Assert.AreEqual(top.y, top.w, 1e-5f);
            Assert.AreEqual(bottom.y, bottom.w, 1e-5f);
        }
    }
}
