using DracoRuan.Foundation.DataFlow.SaveSystem;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public interface IDataProviderService
    {
        public IDataProvider GetDataProviderByType(DataProviderType dataProviderType);
        
        public IDataSaveService GetDataSaveServiceByType(DataProviderType dataProviderType);
    }
}
