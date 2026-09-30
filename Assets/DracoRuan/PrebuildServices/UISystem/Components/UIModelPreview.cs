using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components
{
    /// <summary>
    /// Shows a 3D model inside a Screen Space - Overlay canvas (character preview, item
    /// inspect): a private camera renders the model into a RenderTexture that this RawImage
    /// displays (CAMERA_INPUT_PLAN.md, Quyết định 3). The model is placed far from the origin on
    /// its own stage; put it on <see cref="cullingMask"/> layers (and light it with lights
    /// under the stage) so gameplay cameras and the preview do not see each other.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class UIModelPreview : MonoBehaviour
    {
        private static readonly Vector3 StageOffset = new Vector3(0f, -5000f, 0f);

        [SerializeField] private Vector2Int textureSize = new Vector2Int(512, 512);
        [SerializeField] private Color background = Color.clear;
        [SerializeField] private LayerMask cullingMask = ~0;
        [SerializeField, Range(1f, 120f)] private float fieldOfView = 30f;
        [SerializeField] private float distance = 3f;
        [SerializeField] private Vector3 modelOffset = Vector3.zero;

        private RawImage _image;
        private RenderTexture _texture;
        private Transform _stage;
        private Camera _camera;
        private GameObject _model;

        public Camera PreviewCamera
        {
            get
            {
                this.EnsureRig();
                return this._camera;
            }
        }

        public RenderTexture Texture
        {
            get
            {
                this.EnsureRig();
                return this._texture;
            }
        }

        public GameObject Model => this._model;

        /// <summary>Replaces the shown model with an instance of <paramref name="prefab"/>.</summary>
        public GameObject SetModel(GameObject prefab)
        {
            this.Clear();
            if (!prefab)
                return null;

            this.EnsureRig();
            this._model = Object.Instantiate(prefab, this._stage);
            this._model.transform.localPosition = this.modelOffset;
            this._model.transform.localRotation = Quaternion.identity;
            return this._model;
        }

        /// <summary>Turns the model around the vertical axis, for drag-to-rotate.</summary>
        public void SetYaw(float degrees)
        {
            if (this._model)
                this._model.transform.localRotation = Quaternion.Euler(0f, degrees, 0f);
        }

        public void Clear()
        {
            if (this._model)
                Object.Destroy(this._model);

            this._model = null;
        }

        private void EnsureRig()
        {
            if (this._stage)
                return;

            this._image = this.GetComponent<RawImage>();

            var stageGo = new GameObject($"{this.name}_ModelPreviewStage") { hideFlags = HideFlags.DontSave };
            this._stage = stageGo.transform;
            this._stage.position = StageOffset;

            var cameraGo = new GameObject("PreviewCamera", typeof(Camera));
            cameraGo.transform.SetParent(this._stage, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -this.distance);
            this._camera = cameraGo.GetComponent<Camera>();
            this._camera.clearFlags = CameraClearFlags.SolidColor;
            this._camera.backgroundColor = this.background;
            this._camera.cullingMask = this.cullingMask;
            this._camera.fieldOfView = this.fieldOfView;
            this._camera.nearClipPlane = 0.05f;
            this._camera.farClipPlane = this.distance * 4f;

            this._texture = new RenderTexture(Mathf.Max(16, this.textureSize.x), Mathf.Max(16, this.textureSize.y), 24,
                RenderTextureFormat.ARGB32) { name = "UIModelPreview" };
            this._camera.targetTexture = this._texture;
            this._image.texture = this._texture;
        }

        private void OnDestroy()
        {
            if (this._camera)
                this._camera.targetTexture = null;

            if (this._texture)
            {
                this._texture.Release();
                Object.Destroy(this._texture);
            }

            if (this._stage)
                Object.Destroy(this._stage.gameObject);
        }
    }
}
