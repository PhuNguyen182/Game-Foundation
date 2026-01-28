namespace DracoRuan.CoreSystems.DesignPatterns.ChainOfResponsibility.WithData
{
    public abstract class BaseHandler<TRequest, TResponse> : IHandler<TRequest, TResponse> 
    {
        private IHandler<TRequest, TResponse> _nextHandler;

        public abstract bool CanHandle(TRequest request);
        
        public IHandler<TRequest, TResponse> SetNext(IHandler<TRequest, TResponse> handler)
        {
            this._nextHandler = handler;
            return handler;
        }

        public virtual TResponse Handle(TRequest request)
        {
            return this.Continue(request);
        }

        public virtual TResponse Continue(TRequest request)
        {
            return this._nextHandler != null ? this._nextHandler.Handle(request) : default;
        }
    }
} 