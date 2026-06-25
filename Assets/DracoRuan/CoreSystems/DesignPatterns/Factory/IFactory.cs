using System;

namespace DracoRuan.CoreSystems.DesignPatterns.Factory
{
    public interface IFactory : IDisposable
    {
        
    }

    public interface IFactory<out TResult> : IFactory
    {
        public TResult Create();
    }
    
    public interface IFactory<in TArg, out TResult> : IFactory
    {
        public TResult Create(TArg arg);
    }

    public interface IFactory<in TArg1, in TArg2, out TResult> : IFactory
    {
        public TResult Create(TArg1 arg1, TArg2 arg2);
    }

    public interface IFactory<in TArg1, in TArg2, in TArg3, out TResult> : IFactory
    {
        public TResult Create(TArg1 arg1, TArg2 arg2, TArg3 arg3);
    }

    public interface IFactory<in TArg1, in TArg2, in TArg3, in TArg4, out TResult> : IFactory
    {
        public TResult Create(TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4);
    }
}
