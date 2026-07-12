#if USE_EXTENDED_ADDRESSABLE
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces
{
    public interface IAssetBundleUpdater : IDisposable
    {
        public UniTask<List<string>> CheckForUpdates();

        public UniTask UpdateCatalogs(bool autoCleanBundleCached = true, bool autoRelease = true,
            bool wantToPreserve = false, Action onUpdateComplete = null, Action onUpdateFailed = null);
    }
}
#endif
