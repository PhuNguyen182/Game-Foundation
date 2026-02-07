using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class FirebaseRemoteConfigDataProcessor<TData> : IProcessSequence, IProcessSequenceData
    where TData : IGameData
    {
        private readonly string _remoteConfigKey;
        private readonly IDataSerializer<TData> _dataSerializer;
        
        public IGameData GameData { get; private set; }

        public FirebaseRemoteConfigDataProcessor(string remoteConfigKey)
        {
            this._remoteConfigKey = remoteConfigKey;
            this._dataSerializer = new JsonDataSerializer<TData>();
        }

        public async UniTask<bool> Process()
        {
            await UniTask.CompletedTask;
            // To do: This part of the function should add logic to get remote data from Firebase Remote Config
            // If the remote config get the desired value from the passed key successfully, set the IsFinished to true 
            // Firebase remote config or any remote config always use JSON serializer, so does not matter what ICustomSerializer are using
            return true;
        }

    }
}
