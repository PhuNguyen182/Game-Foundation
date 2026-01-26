using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.Foundation.DataFlow.TypeCreator;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService
{
    /// <summary>
    /// Use this class to save data to PlayerPrefs.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PlayerPrefDataSaveService<T> : IDataSaveService<T>
    {
        private readonly IDataSerializer<T> _dataSerializer;

        public PlayerPrefDataSaveService(IDataSerializer<T> dataSerializer)
        {
            this._dataSerializer = dataSerializer;
        }

        public async UniTask<T> LoadData(string name)
        {
            if (!PlayerPrefs.HasKey(name))
                return TypeFactory.Create<T>();

            await UniTask.CompletedTask;
            string serializedData = PlayerPrefs.GetString(name);
            T data = this._dataSerializer.Deserialize(serializedData);
            return data;
        }

        public UniTask SaveDataAsync(string name, T data)
        {
            string serializedData = this._dataSerializer.Serialize(data) as string;
            PlayerPrefs.SetString(name, serializedData);
            return UniTask.CompletedTask;
        }

        public void SaveData(string name, T data)
        {
            string serializedData = this._dataSerializer.Serialize(data) as string;
            PlayerPrefs.SetString(name, serializedData);
        }

        public void DeleteData(string name) => PlayerPrefs.DeleteKey(name);
    }
}
