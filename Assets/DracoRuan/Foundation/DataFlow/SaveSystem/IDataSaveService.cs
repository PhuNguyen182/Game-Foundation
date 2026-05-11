using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.SaveSystem
{
    public interface IDataSaveService
    {
        public bool IsDataExist(string dataName);
        public UniTask<string> LoadData(string name);
        public void SaveData(string name, object serializedData);
        public void DeleteData(string name);
    }
}
