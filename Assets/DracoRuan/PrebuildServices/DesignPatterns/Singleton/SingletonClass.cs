namespace DracoRuan.CoreSystems.DesignPatterns.Singleton
{
    public class SingletonClass<TInstance> where TInstance : class, new()
    {
        public static TInstance Instance { get; } = new();
    }
}