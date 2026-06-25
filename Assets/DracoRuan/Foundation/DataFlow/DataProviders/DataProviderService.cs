using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService;
using DracoRuan.RemoteConfig;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public class DataProviderService : IDataProviderService
    {
        #region Data SaveServices

        private readonly IDataSaveService _fileDataSaveService = new FileDataSaveService();
        private readonly IDataSaveService _playerPrefDataSaveService = new PlayerPrefDataSaveService();

        #endregion
        
        #region Data Providers
        
        private readonly IDataProvider _firebaseRemoteConfigDataProvider;
        private readonly IDataProvider _resourcesDataProvider = new ResourcesDataProvider();
        private readonly IDataProvider _addressableDataProvider = new AddressableDataProvider();
        private readonly IDataProvider _playerPrefDataProvider = new PlayerPrefDataProvider();
        private readonly IDataProvider _fileDataProvider = new FileDataProvider();

        #endregion

        public DataProviderService(IRemoteConfigService remoteConfigService)
        {
            this._firebaseRemoteConfigDataProvider = new RemoteConfigDataProvider(remoteConfigService);
        }

        public IDataProvider GetDataProviderByType(DataSourceType dataSourceType)
        {
            IDataProvider dataProvider = dataSourceType switch
            {
                DataSourceType.Resources => this._resourcesDataProvider,
                DataSourceType.Addressable => this._addressableDataProvider,
                DataSourceType.PlayerPrefs => this._playerPrefDataProvider,
                DataSourceType.File => this._fileDataProvider,
                DataSourceType.RemoteConfig => this._firebaseRemoteConfigDataProvider,
                _ => null
            };
            
            return dataProvider;
        }

        public IDataSaveService GetDataSaveServiceByType(DataSourceType dataSourceType)
        {
            IDataSaveService dataSaveService = dataSourceType switch
            {
                DataSourceType.PlayerPrefs => this._playerPrefDataSaveService,
                DataSourceType.File => this._fileDataSaveService,
                _ => null
            };
            
            return dataSaveService;
        }
    }
}
