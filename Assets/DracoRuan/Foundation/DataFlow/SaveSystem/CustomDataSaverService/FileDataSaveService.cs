using System.IO;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.Foundation.DataFlow.TypeCreator;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService
{
    /// <summary>
    /// Use this class to save data to files.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FileDataSaveService<T> : IDataSaveService<T>
    {
        private const string LocalDataPrefix = "GameData";
        
        private readonly IDataSerializer<T> _dataSerializer;
        private readonly string _filePath;
        private readonly string _fileExtension;

        public FileDataSaveService(IDataSerializer<T> dataSerializer)
        {
            this._dataSerializer = dataSerializer;
            this._filePath = Application.persistentDataPath;
            this._fileExtension = this._dataSerializer.FileExtension;
        }

        public async UniTask<T> LoadData(string name)
        {
            string dataPath = this.GetDataPath(name);
            if (!File.Exists(dataPath))
                return TypeFactory.Create<T>();

            using StreamReader streamReader = new(dataPath);
            string serializedData = await streamReader.ReadToEndAsync();
            T data = this._dataSerializer.Deserialize(serializedData);
            return data;
        }

        public async UniTask SaveDataAsync(string name, T data)
        {
            string dataPath = this.GetDataPath(name);
            string directoryPath = this.GetDirectoryPath();
            
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string serializedData = this._dataSerializer.Serialize(data) as string;
            await using FileStream fileStream = new(dataPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true);
            await using StreamWriter writer = new(fileStream);
            await writer.WriteLineAsync(serializedData);
        }

        public void SaveData(string name, T data)
        {
            string dataPath = this.GetDataPath(name);
            string directoryPath = this.GetDirectoryPath();
            
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string serializedData = this._dataSerializer.Serialize(data) as string;
            using FileStream fileStream = new(dataPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: false);
            using StreamWriter writer = new(fileStream);
            writer.WriteLineAsync(serializedData);
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
            string dataPath = Path.Combine(this._filePath, LocalDataPrefix, $"{name}{this._fileExtension}");
            return dataPath;
        }
        
        private string GetDirectoryPath() => Path.Combine(this._filePath, LocalDataPrefix);
    }
}
