#if USE_EXTENDED_ADDRESSABLE
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces
{
    public interface IAssetBundleCleaner
    {
        public bool ClearAll();
        public UniTask ClearDependencyCacheBundles(string key, bool autoRelease = true);
        public UniTask ClearDependencyCacheBundles(IEnumerable<string> keys, bool autoRelease = true);
        public UniTask ClearCachedAssetBundles(IEnumerable<string> catalogIds = null);
    }
}
#endif