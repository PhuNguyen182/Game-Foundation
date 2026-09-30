using System.Collections;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core.Loading;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    /// <summary>PlayMode tests for CAMERA_INPUT_PLAN.md: the always-alive UI camera, the single
    /// UISystem-owned EventSystem, and per-scope scaler overrides.</summary>
    public class UIServiceCameraInputTests
    {
        private readonly List<Object> _created = new();
        private UIService _service;
        private IObjectResolver _resolver;

        [SetUp]
        public void SetUp() => UIBaseCameras.Clear();

        [TearDown]
        public void TearDown()
        {
            this._service?.Dispose();
            this._service = null;
            this._resolver?.Dispose();
            this._resolver = null;
            UIBaseCameras.Clear();

            // UIService.Dispose uses Destroy (deferred), so a following test in the same frame
            // would otherwise find the previous run's layers via GameObject.Find.
            GameObject staleRoot;
            while ((staleRoot = GameObject.Find("UISystem")) != null)
                Object.DestroyImmediate(staleRoot);

            foreach (Object created in this._created)
            {
                if (created)
                    Object.DestroyImmediate(created);
            }

            this._created.Clear();
        }

        private UIRootConfig NewConfig(UIRenderMode mode, bool manageEventSystem = true)
        {
            var layer = ScriptableObject.CreateInstance<UILayerDefinition>();
            this._created.Add(layer);
            var layerSerialized = new SerializedObject(layer);
            layerSerialized.FindProperty("layerName").stringValue = "Screen";
            layerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var config = ScriptableObject.CreateInstance<UIRootConfig>();
            this._created.Add(config);
            var serialized = new SerializedObject(config);
            SerializedProperty layers = serialized.FindProperty("layers");
            layers.arraySize = 1;
            layers.GetArrayElementAtIndex(0).objectReferenceValue = layer;
            serialized.FindProperty("renderMode").enumValueIndex = (int)mode;
            serialized.FindProperty("manageEventSystem").boolValue = manageEventSystem;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private UIService NewService(UIRootConfig config)
        {
            this._resolver = new ContainerBuilder().Build();
            this._service = new UIService(config, new UIRegistry(new UIViewDefinition[0]), this._resolver,
                new DirectUIAssetProvider());
            return this._service;
        }

        private Camera NewSceneCamera(string name)
        {
            var go = new GameObject(name, typeof(Camera));
            this._created.Add(go);
            return go.GetComponent<Camera>();
        }

        private Canvas FindLayerCanvas() => this.FindLayerRoot().GetComponent<Canvas>();

        private GameObject FindLayerRoot()
        {
            return GameObject.Find("Layer_Screen");
        }

        [Test]
        public void OverlayMode_CreatesNoUICamera()
        {
            UIService service = this.NewService(this.NewConfig(UIRenderMode.ScreenSpaceOverlay));

            Assert.IsNull(service.CameraController);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, FindLayerCanvas().renderMode);
        }

        [Test]
        public void CameraMode_CanvasesUseTheAliveUICamera()
        {
            UIService service = this.NewService(this.NewConfig(UIRenderMode.ScreenSpaceCamera));

            Assert.IsNotNull(service.CameraController);
            Canvas canvas = FindLayerCanvas();
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.AreSame(service.CameraController.Camera, canvas.worldCamera);
        }

        [UnityTest]
        public IEnumerator CameraMode_WorldCameraUnchangedAcrossSceneLoads()
        {
            UIService service = this.NewService(this.NewConfig(UIRenderMode.ScreenSpaceCamera));
            Camera before = FindLayerCanvas().worldCamera;

            Camera sceneCamera = this.NewSceneCamera("first");
            sceneCamera.gameObject.AddComponent<UIBaseCameraBinder>();
            yield return null;

            Scene added = SceneManager.CreateScene("UISystemTestScene");
            yield return SceneManager.UnloadSceneAsync(added);
            Object.DestroyImmediate(sceneCamera.gameObject);
            yield return null;

            Assert.AreSame(before, FindLayerCanvas().worldCamera);
            Assert.AreSame(before, service.CameraController.Camera);
        }

        [UnityTest]
        public IEnumerator CameraMode_SwitchesBetweenStandaloneAndAttached()
        {
            UIService service = this.NewService(this.NewConfig(UIRenderMode.ScreenSpaceCamera));
            Assert.IsTrue(service.CameraController.IsStandalone);

            Camera gameplay = this.NewSceneCamera("gameplay");
            var binder = gameplay.gameObject.AddComponent<UIBaseCameraBinder>();
            yield return null;
            Assert.AreSame(gameplay, service.CameraController.AttachedBase);

            binder.enabled = false;
            Assert.IsTrue(service.CameraController.IsStandalone);

            binder.enabled = true;
            Assert.AreSame(gameplay, service.CameraController.AttachedBase);
        }

        [UnityTest]
        public IEnumerator NestedBinders_RestorePreviousCamera()
        {
            UIService service = this.NewService(this.NewConfig(UIRenderMode.ScreenSpaceCamera));
            Camera gameplay = this.NewSceneCamera("gameplay");
            gameplay.gameObject.AddComponent<UIBaseCameraBinder>();
            Camera cutscene = this.NewSceneCamera("cutscene");
            var cutsceneBinder = cutscene.gameObject.AddComponent<UIBaseCameraBinder>();
            yield return null;
            Assert.AreSame(cutscene, service.CameraController.AttachedBase);

            cutsceneBinder.enabled = false;

            Assert.AreSame(gameplay, service.CameraController.AttachedBase);
        }

        [UnityTest]
        public IEnumerator EventSystem_OnlyOneActiveAfterExtraSceneEventSystemAppears()
        {
            UIService service = this.NewService(this.NewConfig(UIRenderMode.ScreenSpaceOverlay));
            var extra = new GameObject("SceneEventSystem", typeof(EventSystem));
            this._created.Add(extra);
            yield return null;

            service.EnforceSingleEventSystem();

            Assert.IsFalse(extra.activeSelf);
            Assert.IsTrue(service.EventSystem.gameObject.activeSelf);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
        }

        [Test]
        public void EventSystem_NotCreatedWhenNotManaged()
        {
            UIService service = this.NewService(this.NewConfig(UIRenderMode.ScreenSpaceOverlay, manageEventSystem: false));

            Assert.IsNull(service.EventSystem);
        }

        [Test]
        public void ScopeOverrides_ApplyWhileScopeLivesAndRevertOnDispose()
        {
            UIRootConfig config = this.NewConfig(UIRenderMode.ScreenSpaceOverlay);
            UIService service = this.NewService(config);
            var overrides = ScriptableObject.CreateInstance<UIScopeOverrides>();
            this._created.Add(overrides);
            var serialized = new SerializedObject(overrides);
            serialized.FindProperty("overrideReferenceResolution").boolValue = true;
            serialized.FindProperty("referenceResolution").vector2Value = new Vector2(1920f, 1080f);
            serialized.FindProperty("overrideMatch").boolValue = true;
            serialized.FindProperty("matchWidthOrHeight").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var scaler = this.FindLayerRoot().GetComponent<CanvasScaler>();
            Vector2 originalResolution = scaler.referenceResolution;
            float originalMatch = scaler.matchWidthOrHeight;

            UIScope scope = service.RegisterScope(new UIRegistry(new UIViewDefinition[0]), this._resolver, overrides);

            Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
            Assert.AreEqual(1f, scaler.matchWidthOrHeight);

            scope.Dispose();

            Assert.AreEqual(originalResolution, scaler.referenceResolution);
            Assert.AreEqual(originalMatch, scaler.matchWidthOrHeight);
        }

        [UnityTest]
        public IEnumerator ScopeBaseCamera_RegisteredForScopeLifetime()
        {
            UIService service = this.NewService(this.NewConfig(UIRenderMode.ScreenSpaceCamera));
            Camera sceneCamera = this.NewSceneCamera("scoped");

            UIScope scope = service.RegisterScope(new UIRegistry(new UIViewDefinition[0]), this._resolver,
                baseCamera: sceneCamera);
            yield return null;
            Assert.AreSame(sceneCamera, service.CameraController.AttachedBase);

            scope.Dispose();

            Assert.IsTrue(service.CameraController.IsStandalone);
        }
    }
}
