#if USE_EXTENDED_ADDRESSABLE
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces
{
    public interface IAssetBundleResourceLocator
    {
        public UniTask<bool> IsKeyValid(string key);
        public UniTask<bool> IsKeyValid(List<string> keys);
        public UniTask<long> GetDownloadSize(string key);
        public UniTask<long> GetDownloadSize(List<string> keys);
    }
}
#endif
