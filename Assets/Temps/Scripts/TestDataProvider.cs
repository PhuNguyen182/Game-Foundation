using System;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.DeleteDynamicData;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData;
using DracoRuan.Foundation.DataFlow.DataProviders;
using MessagePipe;
using UnityEngine;

namespace Temps.Scripts
{
    public class TestDataProvider : MonoBehaviour
    {
        [SerializeField] private string sourceDataKey;
        
        private SaveDataEvent _saveDataEvent;
        private DeleteDataEvent _deleteDataEvent;
        private IDataProvider _resourceDataProvider;

        private void Awake()
        {
            this._resourceDataProvider = new ResourcesDataProvider();
            this._saveDataEvent = new SaveDataEvent(null);
            this._deleteDataEvent = new DeleteDataEvent(null);
        }

        private void Start()
        {
            this.GetData().Forget();
        }

        private async UniTask GetData()
        {
            TextAsset textAsset = await this._resourceDataProvider.LoadDataAsync<TextAsset>(this.sourceDataKey);
            if (textAsset)
                UnityEngine.Debug.Log(textAsset.text);
        }
        
        private void SubscribeDataEvents()
        {
            this._saveDataEvent.SaveDataSubscriber.Subscribe(OnSaveDataMessageReceived);
            this._deleteDataEvent.DeleteDataSubscriber.Subscribe(OnDeleteDataMessageReceived);
        }
        
        private void OnSaveDataMessageReceived(SaveDataMessage message)
        {
            
        }

        private void OnDeleteDataMessageReceived(DeleteDataMessage message)
        {
            
        }
    }
}
