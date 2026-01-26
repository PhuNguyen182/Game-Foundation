namespace DracoRuan.Foundation.DataFlow.Serialization
{
    public interface IDataSerializer<T>
    {
        public string FileExtension { get; }
        public object Serialize(T data);
        public T Deserialize(object name);
    }
}
