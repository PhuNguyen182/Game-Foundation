using System.IO;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService
{
    /// <summary>
    /// Use this class to save data to files.
    /// </summary>
    public class FileDataSaveService : IDataSaveService
    {
        private const string FileExtension = ".data";
        private const string LocalDataPrefix = "GameData";
        
        private readonly string _filePath = Application.persistentDataPath;

        public bool IsDataExist(string dataName)
        {
            string dataPath = this.GetDataPath(dataName);
            bool isDataExist = File.Exists(dataPath);
            return isDataExist;
        }

        public byte[] LoadData(string name)
        {
            string dataPath = this.GetDataPath(name);
            if (!File.Exists(dataPath))
                return null;

            using FileStream fileStream = new(dataPath, FileMode.Open, FileAccess.Read, FileShare.None,
                bufferSize: 4096, useAsync: false);
            byte[] serializedData = new byte[fileStream.Length];
            int readBytes = fileStream.Read(serializedData, 0, serializedData.Length);
            Debug.Log($"Read bytes: {readBytes}");
            return serializedData;
        }

        public void SaveData(string name, byte[] serializedData)
        {
            string dataPath = this.GetDataPath(name);
            string directoryPath = this.GetDirectoryPath();
            
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            
            using FileStream fileStream = new(dataPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: false);
            fileStream.Write(serializedData);
        }

        public void DeleteData(string name)
        {
            string dataPath = this.GetDataPath(name);
            if (!File.Exists(dataPath))
                return;
            
            File.Delete(dataPath);
        }
        
        private string GetDataPath(string name)
        {
            string dataPath = Path.Combine(this._filePath, LocalDataPrefix, $"{name}{FileExtension}");
            return dataPath;
        }
        
        private string GetDirectoryPath() => Path.Combine(this._filePath, LocalDataPrefix);
    }
}
