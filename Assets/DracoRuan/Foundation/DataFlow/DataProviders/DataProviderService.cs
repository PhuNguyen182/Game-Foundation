using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public class DataProviderService : IDataProviderService
    {
        #region Data SaveServices

        private readonly IDataSaveService _fileDataSaveService = new FileDataSaveService();
        private readonly IDataSaveService _playerPrefDataSaveService = new PlayerPrefDataSaveService();

        #endregion
        
        #region Data Providers
        
        private readonly IDataProvider _resourcesDataProvider = new ResourcesDataProvider();
        private readonly IDataProvider _addressableDataProvider = new AddressableDataProvider();
        private readonly IDataProvider _playerPrefDataProvider = new PlayerPrefDataProvider();
        private readonly IDataProvider _fileDataProvider = new FileDataProvider();

        #endregion

        public IDataProvider GetDataProviderByType(DataProviderType dataProviderType)
        {
            IDataProvider dataProvider = dataProviderType switch
            {
                DataProviderType.Resources => this._resourcesDataProvider,
                DataProviderType.Addressable => this._addressableDataProvider,
                DataProviderType.PlayerPrefs => this._playerPrefDataProvider,
                DataProviderType.File => this._fileDataProvider,
                _ => null
            };
            
            return dataProvider;
        }

        public IDataSaveService GetDataSaveServiceByType(DataProviderType dataProviderType)
        {
            IDataSaveService dataSaveService = dataProviderType switch
            {
                DataProviderType.PlayerPrefs => this._playerPrefDataSaveService,
                DataProviderType.File => this._fileDataSaveService,
                _ => null
            };
            
            return dataSaveService;
        }
    }
}
