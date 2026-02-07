using System.IO;
using Cysharp.Threading.Tasks;
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

        public async UniTask<string> LoadData(string name)
        {
            string dataPath = this.GetDataPath(name);
            if (!File.Exists(dataPath))
                return null;

            using StreamReader streamReader = new(dataPath);
            string serializedData = await streamReader.ReadToEndAsync();
            return serializedData;
        }

        public async UniTask SaveDataAsync(string name, object serializedData)
        {
            string dataPath = this.GetDataPath(name);
            string directoryPath = this.GetDirectoryPath();
            
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string saveData = serializedData as string;
            await using FileStream fileStream = new(dataPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true);
            await using StreamWriter writer = new(fileStream);
            await writer.WriteLineAsync(saveData);
        }

        public void SaveData(string name, object serializedData)
        {
            string dataPath = this.GetDataPath(name);
            string directoryPath = this.GetDirectoryPath();
            
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string saveData = serializedData as string;
            using FileStream fileStream = new(dataPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: false);
            using StreamWriter writer = new(fileStream);
            writer.WriteLineAsync(saveData);
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
