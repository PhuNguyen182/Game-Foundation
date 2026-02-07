using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService
{
    /// <summary>
    /// Use this class to save data to PlayerPrefs.
    /// </summary>
    public class PlayerPrefDataSaveService : IDataSaveService
    {
        public async UniTask<string> LoadData(string name)
        {
            if (!PlayerPrefs.HasKey(name))
                return null;

            await UniTask.CompletedTask;
            string serializedData = PlayerPrefs.GetString(name);
            return serializedData;
        }

        public UniTask SaveDataAsync(string name, object serializedData)
        {
            string saveData = serializedData as string;
            PlayerPrefs.SetString(name, saveData);
            return UniTask.CompletedTask;
        }

        public void SaveData(string name, object serializedData)
        {
            string saveData = serializedData as string;
            PlayerPrefs.SetString(name, saveData);
        }

        public void DeleteData(string name) => PlayerPrefs.DeleteKey(name);
    }
}
