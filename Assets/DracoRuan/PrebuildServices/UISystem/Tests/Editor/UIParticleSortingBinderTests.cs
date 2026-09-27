using DracoRuan.PrebuildServices.UISystem.Components;
using NUnit.Framework;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class UIParticleSortingBinderTests
    {
        private GameObject _canvasGo;
        private GameObject _particleGo;

        [TearDown]
        public void TearDown()
        {
            if (this._canvasGo != null)
                Object.DestroyImmediate(this._canvasGo);
            if (this._particleGo != null)
                Object.DestroyImmediate(this._particleGo);
        }

        [Test]
        public void ConfigureForTest_SetsRendererSortingOrder_ToCanvasSortingOrderPlusOffset()
        {
            this._canvasGo = new GameObject("Canvas", typeof(Canvas));
            var canvas = this._canvasGo.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            this._particleGo = new GameObject("Particles", typeof(ParticleSystem));
            var renderer = this._particleGo.GetComponent<ParticleSystemRenderer>();
            var binder = this._particleGo.AddComponent<UIParticleSortingBinder>();

            binder.ConfigureForTest(canvas, renderer, sortOffset: 5);

            Assert.AreEqual(105, renderer.sortingOrder);
        }

        [Test]
        public void ConfigureForTest_NegativeOffset_SubtractsFromCanvasSortingOrder()
        {
            this._canvasGo = new GameObject("Canvas", typeof(Canvas));
            var canvas = this._canvasGo.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;

            this._particleGo = new GameObject("Particles", typeof(ParticleSystem));
            var renderer = this._particleGo.GetComponent<ParticleSystemRenderer>();
            var binder = this._particleGo.AddComponent<UIParticleSortingBinder>();

            binder.ConfigureForTest(canvas, renderer, sortOffset: -3);

            Assert.AreEqual(47, renderer.sortingOrder);
        }
    }
}
