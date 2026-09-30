using System.Collections;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class UIModelPreviewTests
    {
        private GameObject _host;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            this._host = new GameObject("preview", typeof(RectTransform), typeof(RawImage));
            this._prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            this._prefab.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this._host);
            Object.DestroyImmediate(this._prefab);
        }

        [Test]
        public void SetModel_RendersIntoTheRawImageTexture()
        {
            var preview = this._host.AddComponent<UIModelPreview>();

            GameObject model = preview.SetModel(this._prefab);

            Assert.IsNotNull(model);
            Assert.AreSame(preview.Texture, this._host.GetComponent<RawImage>().texture);
            Assert.AreSame(preview.Texture, preview.PreviewCamera.targetTexture);
            Assert.Less(model.transform.position.y, -1000f, "The model lives on a far-away stage.");
        }

        [UnityTest]
        public IEnumerator Clear_DestroysTheModel()
        {
            var preview = this._host.AddComponent<UIModelPreview>();
            GameObject model = preview.SetModel(this._prefab);

            preview.Clear();
            yield return null;

            Assert.IsTrue(model == null);
            Assert.IsNull(preview.Model);
        }

        [UnityTest]
        public IEnumerator Destroy_ReleasesTextureAndStage()
        {
            var preview = this._host.AddComponent<UIModelPreview>();
            preview.SetModel(this._prefab);
            Camera previewCamera = preview.PreviewCamera;
            RenderTexture texture = preview.Texture;

            Object.Destroy(this._host);
            yield return null;

            Assert.IsTrue(previewCamera == null);
            Assert.IsTrue(texture == null);
        }
    }
}
