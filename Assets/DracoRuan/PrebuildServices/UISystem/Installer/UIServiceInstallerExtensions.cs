using System;
using System.Collections.Generic;
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
        /// <summary>Registers the root UIService (as IUINavigator), its registry, and every view model in the collections (Transient).</summary>
        public static void AddUIService(this IContainerBuilder builder, UIRootConfig rootConfig,
            params UIViewCollection[] viewCollections) =>
            builder.AddUIService(rootConfig, (IEnumerable<UIViewCollection>)viewCollections);

        /// <summary>
        /// Registers the root UIService from any number of collections, merged into one registry.
        /// A definition listed in more than one collection is registered once; two different
        /// definitions claiming the same view model type still fail loudly in UIRegistry.
        /// </summary>
        public static void AddUIService(this IContainerBuilder builder, UIRootConfig rootConfig,
            IEnumerable<UIViewCollection> viewCollections)
        {
            List<UIViewDefinition> definitions = MergeDefinitions(viewCollections);

            builder.RegisterInstance(rootConfig);
            builder.RegisterInstance(new UIRegistry(definitions));
#if USE_EXTENDED_ADDRESSABLE
            // AddressableUIAssetProvider degrades to plain direct-reference behavior per
            // definition when its addressablePrefab is unset, so it's a strict superset of
            // DirectUIAssetProvider here - safe to register unconditionally, not per-definition.
            builder.Register<IUIAssetProvider, AddressableUIAssetProvider>(Lifetime.Singleton);
#else
            builder.Register<IUIAssetProvider, DirectUIAssetProvider>(Lifetime.Singleton);
#endif
            builder.Register<UIService>(Lifetime.Singleton).AsSelf().As<IUINavigator>().As<IAsyncInitializable>();

            RegisterViewModels(builder, definitions);
        }

        /// <summary>
        /// Registers scene-local view collections as one UIScope: view models here resolve
        /// from this LifetimeScope's resolver (so they can inject this scene's services), and
        /// closing this scope force-closes any views it opened.
        /// </summary>
        public static void AddUIScope(this IContainerBuilder builder, params UIViewCollection[] viewCollections) =>
            builder.AddUIScope((IEnumerable<UIViewCollection>)viewCollections);

        public static void AddUIScope(this IContainerBuilder builder, IEnumerable<UIViewCollection> viewCollections)
        {
            List<UIViewDefinition> definitions = MergeDefinitions(viewCollections);

            var registry = new UIRegistry(definitions);
            builder.RegisterInstance(registry);
            builder.Register<UIScope>(resolver => resolver.Resolve<UIService>().RegisterScope(registry, resolver),
                Lifetime.Scoped);

            RegisterViewModels(builder, definitions);

            // Force the scope to be created (and thus later disposed with the LifetimeScope)
            // even if nothing ever injects UIScope directly.
            builder.RegisterBuildCallback(resolver => resolver.Resolve<UIScope>());
        }

        /// <summary>Flattens the collections in order, keeping the first occurrence of a
        /// definition that appears in several. A null collection is a wiring mistake, so it
        /// throws instead of silently registering fewer views than the caller expects.</summary>
        private static List<UIViewDefinition> MergeDefinitions(IEnumerable<UIViewCollection> viewCollections)
        {
            if (viewCollections == null)
                throw new ArgumentNullException(nameof(viewCollections));

            var merged = new List<UIViewDefinition>();
            var seen = new HashSet<UIViewDefinition>();

            foreach (UIViewCollection collection in viewCollections)
            {
                if (!collection)
                    throw new InvalidOperationException("A null UIViewCollection was passed to the UI installer.");

                foreach (UIViewDefinition definition in collection.Definitions)
                {
                    // Null entries are kept so UIRegistry reports them with its own message.
                    if (!definition || seen.Add(definition))
                        merged.Add(definition);
                }
            }

            return merged;
        }

        private static void RegisterViewModels(IContainerBuilder builder, IEnumerable<UIViewDefinition> definitions)
        {
            foreach (UIViewDefinition definition in definitions)
            {
                Type viewModelType = definition.ViewModelType;
                if (viewModelType == null)
                    continue;

                builder.Register(viewModelType, Lifetime.Transient).AsSelf();
            }
        }
    }
}