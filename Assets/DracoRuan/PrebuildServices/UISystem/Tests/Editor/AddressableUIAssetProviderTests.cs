using System.Threading;
using DracoRuan.PrebuildServices.UISystem.Core.Loading;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class AddressableUIAssetProviderTests
    {
        private sealed class StandInView : UIViewBase
        {
        }

        private UIViewDefinition _definition;
        private GameObject _prefabStandInObject;
        private UIViewBase _prefabStandIn;

        [SetUp]
        public void SetUp()
        {
            this._prefabStandInObject = new GameObject("PrefabStandIn", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
            this._prefabStandIn = this._prefabStandInObject.AddComponent<StandInView>();
            this._definition = ScriptableObject.CreateInstance<UIViewDefinition>();

            // Prefab is a private SerializeField with no public setter - assign it the same way
            // an Inspector would, through SerializedObject, rather than adding a test-only
            // internal setter to a Data asset whose whole point is designer-authored fields.
            var serialized = new SerializedObject(this._definition);
            serialized.FindProperty("viewPrefab").objectReferenceValue = this._prefabStandIn;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this._prefabStandInObject);
            Object.DestroyImmediate(this._definition);
        }

        [Test]
        public void LoadPrefabAsync_NoAddressableKeySet_ReturnsDirectPrefabReference()
        {
            var provider = new AddressableUIAssetProvider();

            UIViewBase result = provider.LoadPrefabAsync(this._definition, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(this._prefabStandIn, result);
        }

        [Test]
        public void ReleasePrefab_NoAddressableKeySet_DoesNotThrow()
        {
            var provider = new AddressableUIAssetProvider();

            Assert.DoesNotThrow(() => provider.ReleasePrefab(this._definition, this._prefabStandIn));
        }

        [Test]
        public void Dispose_WithNoLeasesTaken_DoesNotThrow()
        {
            var provider = new AddressableUIAssetProvider();

            Assert.DoesNotThrow(() => provider.Dispose());
        }
    }
}
