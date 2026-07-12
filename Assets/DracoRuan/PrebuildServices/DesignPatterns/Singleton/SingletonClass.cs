namespace DracoRuan.PrebuildServices.DesignPatterns.Singleton
{
    public abstract class SingletonClass<TInstance> where TInstance : class, new()
    {
        public static TInstance Instance { get; } = new();
    }
}