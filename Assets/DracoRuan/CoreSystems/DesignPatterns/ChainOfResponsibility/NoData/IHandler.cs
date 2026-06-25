using System;

namespace DracoRuan.CoreSystems.DesignPatterns.ChainOfResponsibility.NoData
{
    public interface IHandler : IDisposable
    {
        public bool CanHandle();
        public IHandler SetNext(IHandler handler);
        public void Handle();
        public void Continue();
    }
} 