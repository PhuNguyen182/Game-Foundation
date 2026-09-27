using System;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.PrebuildServices.UISystem.Core;
using DracoRuan.PrebuildServices.UISystem.Core.Loading;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using VContainer;

namespace DracoRuan.PrebuildServices.UISystem.Installer
{
    public static class UIServiceInstallerExtensions
    {
        /// <summary>Registers the root UIService (as IUINavigator), its registry, and every view model in the collection (Transient).</summary>
        public static void AddUIService(this IContainerBuilder builder, UIRootConfig rootConfig,
            UIViewCollection viewCollection)
        {
            builder.RegisterInstance(rootConfig);
            builder.RegisterInstance(new UIRegistry(viewCollection.Definitions));
#if USE_EXTENDED_ADDRESSABLE
            // AddressableUIAssetProvider degrades to plain direct-reference behavior per
            // definition when its addressablePrefab is unset, so it's a strict superset of
            // DirectUIAssetProvider here - safe to register unconditionally, not per-definition.
            builder.Register<IUIAssetProvider, AddressableUIAssetProvider>(Lifetime.Singleton);
#else
            builder.Register<IUIAssetProvider, DirectUIAssetProvider>(Lifetime.Singleton);
#endif
            builder.Register<UIService>(Lifetime.Singleton).AsSelf().As<IUINavigator>().As<IAsyncInitializable>();

            RegisterViewModels(builder, viewCollection);
        }

        /// <summary>
        /// Registers a scene-local view collection as its own UIScope: view models here resolve
        /// from this LifetimeScope's resolver (so they can inject this scene's services), and
        /// closing this scope force-closes any views it opened.
        /// </summary>
        public static void AddUIScope(this IContainerBuilder builder, UIViewCollection viewCollection)
        {
            var registry = new UIRegistry(viewCollection.Definitions);
            builder.RegisterInstance(registry);
            builder.Register<UIScope>(resolver => resolver.Resolve<UIService>().RegisterScope(registry, resolver),
                Lifetime.Scoped);

            RegisterViewModels(builder, viewCollection);

            // Force the scope to be created (and thus later disposed with the LifetimeScope)
            // even if nothing ever injects UIScope directly.
            builder.RegisterBuildCallback(resolver => resolver.Resolve<UIScope>());
        }

        private static void RegisterViewModels(IContainerBuilder builder, UIViewCollection viewCollection)
        {
            foreach (UIViewDefinition definition in viewCollection.Definitions)
            {
                Type viewModelType = definition.ViewModelType;
                if (viewModelType == null)
                    continue;

                builder.Register(viewModelType, Lifetime.Transient).AsSelf();
            }
        }
    }
}