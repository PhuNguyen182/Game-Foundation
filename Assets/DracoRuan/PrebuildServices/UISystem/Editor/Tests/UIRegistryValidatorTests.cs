using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Editor.Validation;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.Testing;
using DracoRuan.PrebuildServices.UISystem.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Editor.Tests
{
    public class UIRegistryValidatorTests
    {
        private sealed class FakeViewModel : UIViewModel
        {
            public FakeViewModel(IUINavigator navigator) : base(navigator)
            {
            }
        }

        private sealed class OtherFakeViewModel : UIViewModel
        {
            public OtherFakeViewModel(IUINavigator navigator) : base(navigator)
            {
            }
        }

        private sealed class FakeView : UIView<FakeViewModel>
        {
            protected override void Bind(ref UIBinder binder, FakeViewModel viewModel)
            {
            }
        }

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in this._created)
                if (obj != null)
                    Object.DestroyImmediate(obj);

            this._created.Clear();
        }

        private UIViewDefinition NewDefinition<TVM>() where TVM : UIViewModel
        {
            var definition = ScriptableObject.CreateInstance<UIViewDefinition>();
            this._created.Add(definition);

            var serialized = new SerializedObject(definition);
            SerializedProperty vmTypeProperty = serialized.FindProperty("viewModelType");
            SerializedProperty nameProperty = vmTypeProperty.FindPropertyRelative("assemblyQualifiedName");
            nameProperty.stringValue = typeof(TVM).AssemblyQualifiedName;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }

        private void SetPrefab(UIViewDefinition definition, GameObject prefab)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("viewPrefab").objectReferenceValue = prefab.GetComponent<UIViewBase>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetLayer(UIViewDefinition definition, UILayerDefinition layer)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("layer").objectReferenceValue = layer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private UILayerDefinition NewLayer()
        {
            var layer = ScriptableObject.CreateInstance<UILayerDefinition>();
            this._created.Add(layer);
            return layer;
        }

        private GameObject NewViewPrefab()
        {
            var go = new GameObject("View", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.AddComponent<FakeView>();
            this._created.Add(go);
            return go;
        }

        [Test]
        public void Validate_DuplicateViewModelTypeKey_ReportsError()
        {
            UIViewDefinition a = this.NewDefinition<FakeViewModel>();
            UIViewDefinition b = this.NewDefinition<FakeViewModel>();

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { a, b });

            Assert.IsTrue(findings.Any(f => f.Severity == UIValidationSeverity.Error && f.Message.Contains("Duplicate key")));
        }

        [Test]
        public void Validate_UniqueKeys_NoDuplicateKeyError()
        {
            UIViewDefinition a = this.NewDefinition<FakeViewModel>();
            UIViewDefinition b = this.NewDefinition<OtherFakeViewModel>();

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { a, b });

            Assert.IsFalse(findings.Any(f => f.Message.Contains("Duplicate key")));
        }

        [Test]
        public void Validate_PrefabHasMismatchedUIView_ReportsError()
        {
            UIViewDefinition definition = this.NewDefinition<OtherFakeViewModel>();
            GameObject prefab = this.NewViewPrefab(); // carries UIView<FakeViewModel>, not OtherFakeViewModel
            this.SetPrefab(definition, prefab);

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsTrue(findings.Any(f => f.Severity == UIValidationSeverity.Error && f.Message.Contains("not a UIView<OtherFakeViewModel>")));
        }

        [Test]
        public void Validate_PrefabHasMatchingUIView_NoMismatchError()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();
            GameObject prefab = this.NewViewPrefab();
            this.SetPrefab(definition, prefab);

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsFalse(findings.Any(f => f.Message.Contains("not a UIView<")));
        }

        [Test]
        public void Validate_ViewNotOnPrefabRoot_ReportsError()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();
            var root = new GameObject("Root", typeof(RectTransform));
            this._created.Add(root);
            var child = new GameObject("Child", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
            child.transform.SetParent(root.transform);
            child.AddComponent<FakeView>();
            this.SetPrefab(definition, child);

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsTrue(findings.Any(f => f.Severity == UIValidationSeverity.Error && f.Message.Contains("not on its prefab's root")));
        }

        [Test]
        public void Validate_MissingPrefab_ReportsError()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsTrue(findings.Any(f => f.Severity == UIValidationSeverity.Error && f.Message.Contains("no prefab assigned")));
        }

        [Test]
        public void Validate_MissingLayer_ReportsError()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();
            this.SetPrefab(definition, this.NewViewPrefab());

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsTrue(findings.Any(f => f.Severity == UIValidationSeverity.Error && f.Message.Contains("no layer assigned")));
        }

        [Test]
        public void Validate_FullyConfiguredDefinition_NoFindings()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();
            this.SetPrefab(definition, this.NewViewPrefab());
            this.SetLayer(definition, this.NewLayer());

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsEmpty(findings);
        }

        [Test]
        public void Completeness_KeyInTwoCollections_ReportsError()
        {
            UIViewDefinition a = this.NewDefinition<FakeViewModel>();
            UIViewDefinition b = this.NewDefinition<FakeViewModel>();
            UIViewCollection first = this.NewCollection(a);
            UIViewCollection second = this.NewCollection(b);

            List<UIValidationFinding> findings = UIRegistryCompletenessChecker.Check(
                new[] { first, second }, new[] { a, b });

            Assert.IsTrue(findings.Any(f => f.Message.Contains("more than one collection")));
        }

        [Test]
        public void Completeness_DefinitionInNoCollection_ReportsError()
        {
            UIViewDefinition registered = this.NewDefinition<FakeViewModel>();
            UIViewDefinition orphan = this.NewDefinition<OtherFakeViewModel>();
            UIViewCollection collection = this.NewCollection(registered);

            List<UIValidationFinding> findings = UIRegistryCompletenessChecker.Check(
                new[] { collection }, new[] { registered, orphan });

            Assert.IsTrue(findings.Any(f => f.Message.Contains("not registered in any collection") && f.Context == orphan));
            Assert.IsFalse(findings.Any(f => f.Context == registered));
        }

        private UIViewCollection NewCollection(params UIViewDefinition[] definitions)
        {
            var collection = ScriptableObject.CreateInstance<UIViewCollection>();
            this._created.Add(collection);

            var serialized = new SerializedObject(collection);
            SerializedProperty list = serialized.FindProperty("definitions");
            list.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return collection;
        }
    }
}
