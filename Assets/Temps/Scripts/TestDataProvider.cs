using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.PrebuildServices.MessageBrokers.CustomEvents.DeleteDynamicData;
using DracoRuan.PrebuildServices.MessageBrokers.CustomEvents.SaveDynamicData;
using Temps.Scripts.TestRiseProgressData;
using UnityEngine;
using VContainer.Unity;

namespace Temps.Scripts
{
    /// <summary>
    /// Sample consumer of a save repository.
    /// </summary>
    /// <remarks>
    /// <para>Shows the normal usage shape: read <c>Data</c>, mutate it, call
    /// <c>MarkDirty()</c>. The repository is not saved here — the scheduler batches writes and
    /// flushes them on a timer and when the app is backgrounded.</para>
    ///
    /// <para><b>An <c>IStartable</c> taking a repository in its constructor is exactly the case the
    /// gate exists for.</b> VContainer constructs every <c>IStartable</c> well before any
    /// <c>IAsyncStartable</c> runs, so this class comes into existence before migration has even
    /// started. <c>Start()</c> therefore checks <c>IsInitialized</c> rather than assuming the data
    /// is loaded.</para>
    /// </remarks>
    public class TestService : IStartable
    {
        private const string LogTag = "TestService";

        private readonly RiseProgressionDataController _riseProgression;

        public TestService(RiseProgressionDataController riseProgression)
        {
            this._riseProgression = riseProgression;
        }

        public void Start()
        {
            // Constructed before the boot pipeline has loaded saves, so wait for the signal rather
            // than reading Data here.
            this._riseProgression.OnDataLoaded += this.OnRiseProgressionLoaded;
        }

        private void OnRiseProgressionLoaded()
        {
            RiseProgressDataV1 data = this._riseProgression.Data;
            Debug.Log($"[{LogTag}] Rise progression loaded: Number={data.Number}, " +
                      $"Streaks={data.Streaks.Count}");
        }

        /// <summary>Example mutation: change the data, then say so.</summary>
        public void IncrementNumber()
        {
            if (!this._riseProgression.IsInitialized)
                return;

            this._riseProgression.Data.Number++;

            // Queues the write; the scheduler decides when it actually hits disk.
            this._riseProgression.MarkDirty();
        }
    }

    public class TestDataProvider : MonoBehaviour
    {
        [SerializeField] private string sourceDataKey;

        private SaveDataEvent _saveDataEvent;
        private DeleteDataEvent _deleteDataEvent;
        private IDataProvider _resourceDataProvider;

        private void Start()
        {
            //this.GetData().Forget();
        }

        private async UniTask GetData()
        {
            TextAsset textAsset = await this._resourceDataProvider.LoadDataAsync<TextAsset>(this.sourceDataKey);
            if (textAsset)
                Debug.Log(textAsset.text);
        }

        private void OnSaveDataMessageReceived(SaveDataMessage message)
        {
        }

        private void OnDeleteDataMessageReceived(DeleteDataMessage message)
        {
        }
    }
}