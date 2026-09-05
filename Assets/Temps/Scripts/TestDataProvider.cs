using System.Collections;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.PrebuildServices.MessageBrokers.CustomEvents.DeleteDynamicData;
using DracoRuan.PrebuildServices.MessageBrokers.CustomEvents.SaveDynamicData;
using Temps.Scripts.TestRiseProgressData;
using UnityEngine;
using VContainer.Unity;

namespace Temps.Scripts
{
    public class TestService : IStartable
    {
        private MonoBehaviour _behaviour;
        private readonly RiseProgressionDataController _riseProgressionDataController;

        public TestService(RiseProgressionDataController riseProgressionDataController)
        {
            this._riseProgressionDataController = riseProgressionDataController;
        }

        public void Start()
        {
            _behaviour.StartCoroutine(Coroutine());
            UnityEngine.Debug.Log(this._riseProgressionDataController);
        }

        private IEnumerator Coroutine()
        {
            yield return null;
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
