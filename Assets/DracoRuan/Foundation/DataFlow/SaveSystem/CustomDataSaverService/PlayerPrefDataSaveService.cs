using System.Text;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService
{
    /// <summary>
    /// Use this class to save data to PlayerPrefs.
    /// </summary>
    public class PlayerPrefDataSaveService : IDataSaveService
    {
        public bool IsDataExist(string dataName)
        {
            bool isDataExist = PlayerPrefs.HasKey(dataName);
            return isDataExist;
        }
        
        public byte[] LoadData(string name)
        {
            if (!PlayerPrefs.HasKey(name))
                return null;
            
            string serializedData = PlayerPrefs.GetString(name);
            byte[] data = Encoding.UTF8.GetBytes(serializedData);
            return data;
        }

        public void SaveData(string name, byte[] serializedData)
        {
            string saveData = Encoding.UTF8.GetString(serializedData);
            PlayerPrefs.SetString(name, saveData);
        }

        public void DeleteData(string name) => PlayerPrefs.DeleteKey(name);
    }
}
