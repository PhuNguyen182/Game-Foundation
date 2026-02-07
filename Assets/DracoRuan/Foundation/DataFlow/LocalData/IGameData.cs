namespace DracoRuan.Foundation.DataFlow.LocalData
{
    public interface IGameData
    {
        public int Version { get; set; }
    }

    public interface ISetCustomGameData
    {
        public void SetCustomGameData(object gameData);
    }
}
