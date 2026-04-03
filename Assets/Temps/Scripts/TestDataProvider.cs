using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using UnityEngine;

namespace Temps.Scripts
{
    public class TestDataProvider : MonoBehaviour
    {
        [SerializeField] private string sourceDataKey;
        
        private IDataProvider _resourceDataProvider;

        private void Awake()
        {
            this._resourceDataProvider = new ResourcesDataProvider();
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
    }
}
