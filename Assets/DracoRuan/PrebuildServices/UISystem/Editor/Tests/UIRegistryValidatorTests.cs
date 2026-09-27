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

        /// <summary>Deliberately references a UnityEngine type in a field, so
        /// ValidateViewModelsDoNotUseUnityEngine has something real to flag.</summary>
        private sealed class OffendingViewModel : UIViewModel
        {
            private Transform _target;

            public OffendingViewModel(IUINavigator navigator) : base(navigator)
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
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
        public void Validate_PrefabHasNoUIView_ReportsError()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();
            var prefab = new GameObject("NoView");
            this._created.Add(prefab);
            this.SetPrefab(definition, prefab);

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsTrue(findings.Any(f => f.Severity == UIValidationSeverity.Error && f.Message.Contains("has no UIView<TVM>")));
        }

        [Test]
        public void Validate_PrefabHasMismatchedUIView_ReportsError()
        {
            UIViewDefinition definition = this.NewDefinition<OtherFakeViewModel>();
            GameObject prefab = this.NewViewPrefab(); // carries UIView<FakeViewModel>, not OtherFakeViewModel
            this.SetPrefab(definition, prefab);

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsTrue(findings.Any(f => f.Severity == UIValidationSeverity.Error && f.Message.Contains("has no UIView<OtherFakeViewModel>")));
        }

        [Test]
        public void Validate_PrefabHasMatchingUIView_NoMismatchError()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();
            GameObject prefab = this.NewViewPrefab();
            this.SetPrefab(definition, prefab);

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsFalse(findings.Any(f => f.Message.Contains("has no UIView<")));
        }

        [Test]
        public void Validate_RaycastTargetWithNoInteractiveComponent_ReportsWarning()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();
            GameObject prefab = this.NewViewPrefab();
            var childGo = new GameObject("DecorativeImage", typeof(RectTransform), typeof(Image));
            childGo.transform.SetParent(prefab.transform);
            childGo.GetComponent<Image>().raycastTarget = true;
            this._created.Add(childGo);
            this.SetPrefab(definition, prefab);

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsTrue(findings.Any(f => f.Severity == UIValidationSeverity.Warning && f.Message.Contains("raycastTarget enabled")));
        }

        [Test]
        public void Validate_NestedLayoutGroups_ReportsWarning()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();
            GameObject prefab = this.NewViewPrefab();
            var outerGo = new GameObject("Outer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            outerGo.transform.SetParent(prefab.transform);
            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            innerGo.transform.SetParent(outerGo.transform);
            this._created.Add(outerGo);
            this._created.Add(innerGo);
            this.SetPrefab(definition, prefab);

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsTrue(findings.Any(f => f.Severity == UIValidationSeverity.Warning && f.Message.Contains("nested inside another LayoutGroup")));
        }

        [Test]
        public void Validate_MaskComponent_RecommendsRectMask2D()
        {
            UIViewDefinition definition = this.NewDefinition<FakeViewModel>();
            GameObject prefab = this.NewViewPrefab();
            var maskGo = new GameObject("Masked", typeof(RectTransform), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(prefab.transform);
            this._created.Add(maskGo);
            this.SetPrefab(definition, prefab);

            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new[] { definition });

            Assert.IsTrue(findings.Any(f => f.Message.Contains("RectMask2D")));
        }

        [Test]
        public void Validate_ViewModelWithUnityEngineField_ReportsWarning()
        {
            List<UIValidationFinding> findings = UIRegistryValidator.Validate(new UIViewDefinition[0]);

            Assert.IsTrue(findings.Any(f => f.Message.Contains(nameof(OffendingViewModel)) && f.Message.Contains("UnityEngine")));
        }
    }
}
