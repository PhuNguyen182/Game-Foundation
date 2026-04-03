using DracoRuan.Foundation.DataFlow.SaveSystem;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public interface IDataProviderService
    {
        public IDataProvider GetDataProviderByType(DataSourceType dataSourceType);
        
        public IDataSaveService GetDataSaveServiceByType(DataSourceType dataSourceType);
    }
}
