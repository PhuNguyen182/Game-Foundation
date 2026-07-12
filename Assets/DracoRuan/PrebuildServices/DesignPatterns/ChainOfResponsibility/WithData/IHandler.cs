namespace DracoRuan.CoreSystems.DesignPatterns.ChainOfResponsibility.WithData
{
    public interface IHandler<TRequest, TResponse>
    {
        public bool CanHandle(TRequest request);
        public IHandler<TRequest, TResponse> SetNext(IHandler<TRequest, TResponse> handler);
        public TResponse Handle(TRequest request);
        public TResponse Continue(TRequest request);
    }
} 