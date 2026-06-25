using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.RemoteConfig;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public class RemoteConfigDataProvider : IDataProvider
    {
        private const string LogKey = "RemoteConfigDataProvider";
        
        private readonly IRemoteConfigService _remoteConfigService;

        public RemoteConfigDataProvider(IRemoteConfigService remoteConfigService)
        {
            this._remoteConfigService = remoteConfigService;
        }
        
        public async UniTask<TData> LoadDataAsync<TData>(string pathToData, IDataSerializer<TData> serializer = null,
            IDataSaveService dataSaveService = null)
        {
            try
            {
                if (serializer == null)
                {
                    Debug.LogError($"{LogKey} Serializer of type {typeof(IDataSerializer<TData>)} cannot be null.");
                    return default;
                }
                    
                string remoteConfigPath = this._remoteConfigService.GetStringValue(pathToData);
                TData data = serializer.Deserialize(remoteConfigPath);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            
            await UniTask.CompletedTask;
            return default;
        }

        public void UnloadData<TData>(TData data)
        {
            
        }
    }
}
