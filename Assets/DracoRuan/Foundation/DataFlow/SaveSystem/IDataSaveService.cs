namespace DracoRuan.Foundation.DataFlow.SaveSystem
{
    public interface IDataSaveService
    {
        public bool IsDataExist(string dataName);
        public byte[] LoadData(string name);
        public void SaveData(string name, byte[] serializedData);
        public void DeleteData(string name);
    }
}
