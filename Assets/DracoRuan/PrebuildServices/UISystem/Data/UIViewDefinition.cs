using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;
#if USE_EXTENDED_ADDRESSABLE
using UnityEngine.AddressableAssets;
#endif

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
        [SerializeField] [TypeConstraint(typeof(UIViewModel))]
        private SerializableTypeRef viewModelType;

        [SerializeField] private UILayerDefinition layer;
        [SerializeField] private UIViewPreset preset;

        /// <summary>Typed as the root view component (not GameObject) so the router instantiates
        /// and gets the view back in one step, with no runtime GetComponent/convert.</summary>
        [SerializeField] private UIViewBase viewPrefab;

#if USE_EXTENDED_ADDRESSABLE
        /// <summary>Addressables alternative to `viewPrefab` (REWRITE_PLAN.md 2.2: "Prefab: direct
        /// reference hoặc AssetReference"). Set this and leave `viewPrefab` null to load
        /// through Addressables instead - AddressableUIAssetProvider prefers this when both are
        /// somehow set, though authoring only one at a time is the expectation.</summary>
        [SerializeField] private AssetReferenceUIView addressablePrefab;
#endif
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

        /// <summary>HUD-only (REWRITE_PLAN.md 2.5, "hideMode, không destroy"): screen VM types
        /// this HUD should be visible on. Empty = always visible whenever open, no restriction.
        /// The router re-evaluates this against the current topmost screen every time the
        /// screen stack changes (see UIService.RefreshHudVisibility).</summary>
        [SerializeField] [TypeConstraint(typeof(UIViewModel))]
        private SerializableTypeRef[] visibleOnScreens = Array.Empty<SerializableTypeRef>();

        public Type ViewModelType => this.viewModelType?.ResolveType();
        public UILayerDefinition Layer => this.layer;
        public UIViewPreset Preset => this.preset;
        public UIViewBase Prefab => this.viewPrefab;

        /// <summary>True when a direct prefab or a valid Addressables reference is assigned.</summary>
        public bool HasPrefabSource
        {
            get
            {
                if (this.viewPrefab)
                    return true;
#if USE_EXTENDED_ADDRESSABLE
                return this.addressablePrefab != null && this.addressablePrefab.RuntimeKeyIsValid();
#else
                return false;
#endif
            }
        }

#if USE_EXTENDED_ADDRESSABLE
        public AssetReferenceUIView AddressablePrefab => this.addressablePrefab;
#endif
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

        /// <summary>Resolved screen VM types this HUD is restricted to. Empty = always visible.
        /// Resolves lazily every call rather than caching, matching ViewModelType's own
        /// resolve-on-read pattern above (definitions are load-once SO assets, so the cost is
        /// negligible against router traffic).</summary>
        public IEnumerable<Type> VisibleOnScreens
        {
            get
            {
                foreach (SerializableTypeRef typeRef in this.visibleOnScreens)
                {
                    Type resolved = typeRef?.ResolveType();
                    if (resolved != null)
                        yield return resolved;
                }
            }
        }

        public bool HasVisibleOnScreensRestriction => this.visibleOnScreens.Length > 0;
    }
}