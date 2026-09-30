using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components;
using NUnit.Framework;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class SafeAreaFitterTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (this._go != null)
                Object.DestroyImmediate(this._go);
        }

        [Test]
        public void Apply_WithFullScreenSafeArea_StretchesToFullAnchors()
        {
            // The Editor's Game view reports Screen.safeArea == the full screen (no notch to
            // simulate here), so the one thing provable without mocking Screen is the
            // full-safe-area identity case: anchors end up (0,0)-(1,1), same as no fitter at
            // all. The pixel/anchor conversion math itself (partial safe area) needs a device
            // or simulator that actually reports a non-full safeArea - out of reach here.
            this._go = new GameObject("SafeArea", typeof(RectTransform));
            var rect = (RectTransform)this._go.transform;
            rect.anchorMin = new Vector2(0.2f, 0.2f);
            rect.anchorMax = new Vector2(0.8f, 0.8f);
            var fitter = this._go.AddComponent<SafeAreaFitter>();

            fitter.ApplyForTest();

            Assert.AreEqual(Vector2.zero, rect.anchorMin);
            Assert.AreEqual(Vector2.one, rect.anchorMax);
            Assert.AreEqual(Vector2.zero, rect.offsetMin);
            Assert.AreEqual(Vector2.zero, rect.offsetMax);
        }
    }
}
