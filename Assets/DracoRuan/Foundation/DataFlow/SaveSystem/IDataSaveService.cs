using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.SaveSystem
{
    public interface IDataSaveService
    {
        public UniTask<string> LoadData(string name);
        public UniTask SaveDataAsync(string name, object serializedData);
        public void SaveData(string name, object serializedData);
        public void DeleteData(string name);
    }
}
