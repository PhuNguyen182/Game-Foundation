using System;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.DeleteDynamicData;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData;
using DracoRuan.Foundation.DataFlow.DataProviders;
using MessagePipe;
using Temps.Scripts.TestRiseProgressData;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

namespace Temps.Scripts
{
    public class TestService : IStartable
    {
        private readonly RiseProgressionDataController _riseProgressionDataController;

        public TestService(RiseProgressionDataController riseProgressionDataController)
        {
            this._riseProgressionDataController = riseProgressionDataController;
        }

        public void Start()
        {
            UnityEngine.Debug.Log(this._riseProgressionDataController);
        }
    }
    
    public class TestDataProvider : MonoBehaviour
    {
        [SerializeField] private string sourceDataKey;
        
        private SaveDataEvent _saveDataEvent;
        private DeleteDataEvent _deleteDataEvent;
        private IDataProvider _resourceDataProvider;

        private void Awake()
        {
            
        }

        private void Start()
        {
            //this.GetData().Forget();
        }

        private async UniTask GetData()
        {
            TextAsset textAsset = await this._resourceDataProvider.LoadDataAsync<TextAsset>(this.sourceDataKey);
            if (textAsset)
                UnityEngine.Debug.Log(textAsset.text);
        }
        
        private void SubscribeDataEvents()
        {
            
        }
        
        private void OnSaveDataMessageReceived(SaveDataMessage message)
        {
            
        }

        private void OnDeleteDataMessageReceived(DeleteDataMessage message)
        {
            
        }
    }
}
