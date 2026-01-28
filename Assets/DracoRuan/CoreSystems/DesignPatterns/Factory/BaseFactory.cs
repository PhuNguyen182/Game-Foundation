namespace DracoRuan.CoreSystems.DesignPatterns.Factory
{
    // Use abstract class instead of interface to prevent potential memory leaking
    public abstract class BaseFactory<TResult> : IFactory<TResult>
    {
        public abstract TResult Produce();
    }
    
    public abstract class BaseFactory<TArg, TResult> : IFactory<TArg, TResult>
    {
        public abstract TResult Produce(TArg arg);
    }
    
    public abstract class BaseFactory<TArg1, TArg2, TResult> : IFactory<TArg1, TArg2, TResult>
    {
        public abstract TResult Produce(TArg1 arg1, TArg2 arg2);
    }
    
    public abstract class BaseFactory<TArg1, TArg2, TArg3, TResult> : IFactory<TArg1, TArg2, TArg3, TResult>
    {
        public abstract TResult Produce(TArg1 arg1, TArg2 arg2, TArg3 arg3);
    }
    
    public abstract class BaseFactory<TArg1, TArg2, TArg3, TArg4, TResult> : IFactory<TArg1, TArg2, TArg3, TArg4, TResult>
    {
        public abstract TResult Produce(TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4);
    }
}
