#if USE_EXTENDED_ADDRESSABLE
using System;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime
{
    public interface IAssetBundleService : IDisposable
    {
        public IAssetBundleLoader AssetBundleLoader { get; }
        public IAssetBundleCleaner AssetBundleCleaner { get; }
        public IAssetBundleUpdater AssetBundleUpdater { get; }
        public IAssetBundleResourceLocator AssetBundleResourceLocator { get; }
        public IAssetBundleDownloader AssetBundleDownloader { get; }

        public UniTask<bool> Initialize(Action onInitializationComplete = null,
            Action onInitializationFailed = null);
        
        public UniTask<bool> InitializeAsync(UniTask onInitializationComplete,
            UniTask onInitializationFailed);
    }
}
#endif