namespace DracoRuan.CoreSystems.DesignPatterns.Factory
{
    public interface IFactory
    {
        
    }

    public interface IFactory<out TResult> : IFactory
    {
        public TResult Produce();
    }
    
    public interface IFactory<in TArg, out TResult> : IFactory
    {
        public TResult Produce(TArg arg);
    }

    public interface IFactory<in TArg1, in TArg2, out TResult> : IFactory
    {
        public TResult Produce(TArg1 arg1, TArg2 arg2);
    }

    public interface IFactory<in TArg1, in TArg2, in TArg3, out TResult> : IFactory
    {
        public TResult Produce(TArg1 arg1, TArg2 arg2, TArg3 arg3);
    }

    public interface IFactory<in TArg1, in TArg2, in TArg3, in TArg4, out TResult> : IFactory
    {
        public TResult Produce(TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4);
    }
}
