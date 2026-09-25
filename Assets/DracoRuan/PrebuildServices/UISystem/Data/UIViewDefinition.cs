using System;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>
    /// One entry in a UIViewCollection: everything the router needs to open a view model's
    /// view. Key = ViewModelType. Preset only fills defaults for the flags below; each flag
    /// can still be tuned per-view (e.g. a full-screen popup that still behaves like a popup
    /// for Back/backdrop purposes).
    /// </summary>
    [CreateAssetMenu(fileName = "UIViewDefinition", menuName = "DracoRuan/UISystem/View Definition")]
    public sealed class UIViewDefinition : ScriptableObject
    {
        [SerializeField] private SerializableTypeRef viewModelType;
        [SerializeField] private UILayerDefinition layer;
        [SerializeField] private UIViewPreset preset;
        [SerializeField] private GameObject prefab;
        [SerializeField] private UICachePolicy cachePolicy = UICachePolicy.Destroy;
        [SerializeField] private bool preload;
        [SerializeField] private UIHideMode hideMode = UIHideMode.DisableCanvas;
        [SerializeField] private UIReopenPolicy reopenPolicy = UIReopenPolicy.BringToFront;
        [SerializeField] private bool modal;
        [SerializeField] private bool closeOnBackdrop = true;
        [SerializeField] private int defaultPriority;
        [SerializeField] private UIViewScope scope = UIViewScope.Screen;
        [SerializeField] private bool hidesBelow;
        [SerializeField] private bool history = true;
        [SerializeField] private bool participatesInStack = true;
        [SerializeField] private float speedOverride = 1f;

        public Type ViewModelType => this.viewModelType?.ResolveType();
        public UILayerDefinition Layer => this.layer;
        public UIViewPreset Preset => this.preset;
        public GameObject Prefab => this.prefab;
        public UICachePolicy CachePolicy => this.cachePolicy;
        public bool Preload => this.preload;
        public UIHideMode HideMode => this.hideMode;
        public UIReopenPolicy ReopenPolicy => this.reopenPolicy;
        public bool Modal => this.modal;
        public bool CloseOnBackdrop => this.closeOnBackdrop;
        public int DefaultPriority => this.defaultPriority;
        public UIViewScope Scope => this.scope;
        public bool HidesBelow => this.hidesBelow;
        public bool History => this.history;
        public bool ParticipatesInStack => this.participatesInStack;
        public float SpeedOverride => this.speedOverride;
    }
}
