using System;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public interface ICustomDataManager : IDisposable
    {
        public UniTask InitializeDataHandlers(IMainDataManager mainDataManager);
    }
}
