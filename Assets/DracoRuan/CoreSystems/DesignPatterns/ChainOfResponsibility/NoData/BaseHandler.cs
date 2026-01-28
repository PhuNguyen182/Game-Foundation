namespace DracoRuan.CoreSystems.DesignPatterns.ChainOfResponsibility.NoData
{
    public abstract class BaseHandler : IHandler 
    {
        private IHandler _nextHandler;

        public abstract bool CanHandle();
        
        public IHandler SetNext(IHandler handler)
        {
            this._nextHandler = handler;
            return handler;
        }

        public virtual void Handle()
        {
            this.Continue();
        }

        public virtual void Continue()
        {
            this._nextHandler?.Handle();
        }
    }
} 