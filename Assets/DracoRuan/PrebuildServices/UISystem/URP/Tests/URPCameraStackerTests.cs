using DracoRuan.PrebuildServices.UISystem.URP;
using DracoRuan.PrebuildServices.UISystem.URP.DracoRuan.PrebuildServices.UISystem.URP;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class URPCameraStackerTests
    {
        private GameObject _uiGo;
        private GameObject _baseGo;
        private Camera _ui;
        private Camera _base;
        private URPCameraStacker _stacker;

        [SetUp]
        public void SetUp()
        {
            this._uiGo = new GameObject("ui", typeof(Camera));
            this._baseGo = new GameObject("base", typeof(Camera));
            this._ui = this._uiGo.GetComponent<Camera>();
            this._base = this._baseGo.GetComponent<Camera>();
            this._stacker = new URPCameraStacker();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this._uiGo);
            Object.DestroyImmediate(this._baseGo);
        }

        [Test]
        public void Configure_StripsPostProcessingAndExtraTextures()
        {
            this._stacker.Configure(this._ui);

            UniversalAdditionalCameraData data = this._ui.GetUniversalAdditionalCameraData();
            Assert.IsFalse(data.renderPostProcessing);
            Assert.IsFalse(data.renderShadows);
            Assert.IsFalse(data.requiresDepthTexture);
            Assert.IsFalse(data.requiresColorTexture);
        }

        [Test]
        public void Attach_MakesUICameraOverlayInBaseCameraStack()
        {
            this._stacker.Attach(this._ui, this._base);

            Assert.AreEqual(CameraRenderType.Overlay, this._ui.GetUniversalAdditionalCameraData().renderType);
            CollectionAssert.Contains(this._base.GetUniversalAdditionalCameraData().cameraStack, this._ui);
        }

        [Test]
        public void Attach_Twice_DoesNotDuplicateStackEntry()
        {
            this._stacker.Attach(this._ui, this._base);
            this._stacker.Attach(this._ui, this._base);

            Assert.AreEqual(1, this._base.GetUniversalAdditionalCameraData().cameraStack.Count);
        }

        [Test]
        public void Detach_RemovesFromStackAndBecomesBaseCamera()
        {
            this._stacker.Attach(this._ui, this._base);

            this._stacker.Detach(this._ui, this._base);

            Assert.AreEqual(CameraRenderType.Base, this._ui.GetUniversalAdditionalCameraData().renderType);
            CollectionAssert.DoesNotContain(this._base.GetUniversalAdditionalCameraData().cameraStack, this._ui);
        }

        [Test]
        public void Detach_WithDestroyedBaseCamera_DoesNotThrow()
        {
            this._stacker.Attach(this._ui, this._base);
            Object.DestroyImmediate(this._baseGo);

            Assert.DoesNotThrow(() => this._stacker.Detach(this._ui, null));
        }
    }
}
