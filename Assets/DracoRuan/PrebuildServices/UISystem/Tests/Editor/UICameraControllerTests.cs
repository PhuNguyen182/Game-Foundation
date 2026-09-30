using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core;
using NUnit.Framework;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class UICameraControllerTests
    {
        private sealed class FakeStacker : IUICameraStacker
        {
            public readonly List<string> Calls = new();
            public bool IsSupported => true;
            public void Configure(Camera uiCamera) => this.Calls.Add("configure");
            public void Attach(Camera uiCamera, Camera baseCamera) => this.Calls.Add("attach:" + baseCamera.name);
            public void Detach(Camera uiCamera, Camera baseCamera) =>
                this.Calls.Add("detach:" + (baseCamera ? baseCamera.name : "null"));
        }

        private readonly List<GameObject> _created = new();
        private FakeStacker _stacker;
        private UICameraController _controller;

        private Camera NewCamera(string name)
        {
            var go = new GameObject(name, typeof(Camera));
            this._created.Add(go);
            return go.GetComponent<Camera>();
        }

        [SetUp]
        public void SetUp()
        {
            UIBaseCameras.Clear();
            this._stacker = new FakeStacker();
            this._controller = new UICameraController(this.NewCamera("ui"), this._stacker);
        }

        [TearDown]
        public void TearDown()
        {
            this._controller.Dispose();
            UIBaseCameras.Clear();
            foreach (GameObject go in this._created)
            {
                if (go)
                    Object.DestroyImmediate(go);
            }

            this._created.Clear();
        }

        [Test]
        public void NoBaseCamera_UICameraIsStandaloneSolidBlack()
        {
            Assert.IsTrue(this._controller.IsStandalone);
            Assert.AreEqual(CameraClearFlags.SolidColor, this._controller.Camera.clearFlags);
            Assert.AreEqual(Color.black, this._controller.Camera.backgroundColor);
            Assert.IsNull(this._controller.AttachedBase);
        }

        [Test]
        public void RegisteringBaseCamera_AttachesUICameraOnTop()
        {
            Camera gameplay = this.NewCamera("gameplay");

            UIBaseCameras.Register(gameplay);

            Assert.AreSame(gameplay, this._controller.AttachedBase);
            Assert.IsFalse(this._controller.IsStandalone);
            Assert.AreEqual("attach:gameplay", this._stacker.Calls[^1]);
        }

        [Test]
        public void NestedRegistrations_RestoreInReverseOrder()
        {
            Camera gameplay = this.NewCamera("gameplay");
            Camera cutscene = this.NewCamera("cutscene");

            UIBaseCameras.Register(gameplay);
            UIBaseCameras.Register(cutscene);
            Assert.AreSame(cutscene, this._controller.AttachedBase);

            UIBaseCameras.Unregister(cutscene);
            Assert.AreSame(gameplay, this._controller.AttachedBase);

            UIBaseCameras.Unregister(gameplay);
            Assert.IsTrue(this._controller.IsStandalone);
            Assert.IsNull(this._controller.AttachedBase);
        }

        [Test]
        public void BaseCameraDestroyed_FallsBackToStandalone()
        {
            Camera gameplay = this.NewCamera("gameplay");
            UIBaseCameras.Register(gameplay);

            Object.DestroyImmediate(gameplay.gameObject);
            this._controller.Refresh();

            Assert.IsTrue(this._controller.IsStandalone);
            Assert.AreEqual("detach:null", this._stacker.Calls[^1]);
        }
    }
}
