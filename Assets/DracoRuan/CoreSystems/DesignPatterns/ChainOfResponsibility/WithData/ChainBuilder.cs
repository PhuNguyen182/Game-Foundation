using System;
using System.Collections.Generic;

namespace DracoRuan.CoreSystems.DesignPatterns.ChainOfResponsibility.WithData
{
    public class ChainBuilder<TRequest, TResponse> : IDisposable where TRequest : class where TResponse : class
    {
        private bool _isDisposed;
        private List<IHandler<TRequest, TResponse>> _handlers = new();
        private bool _isBuilt;

        public ChainBuilder<TRequest, TResponse> AddHandler(IHandler<TRequest, TResponse> handler)
        {
            this._handlers.Add(handler);
            return this;
        }

        private void Build()
        {
            this._isBuilt = false;
            if (this._handlers.Count == 0)
            {
                Debug.LogWarning("No handlers added to the chain");
                return;
            }

            for (int i = 0; i < this._handlers.Count - 1; i++)
                this._handlers[i].SetNext(this._handlers[i + 1]);
            
            this._isBuilt = true;
        }

        public void Start(TRequest request)
        {
            if (!this._isBuilt)
                this.Build();
            
            if (this._isBuilt)
                this._handlers[0].Handle(request);
        }

        private void ReleaseManagedResources()
        {
            this._handlers.Clear();
            this._handlers = null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this._isDisposed)
                return;
            
            if (disposing)
                this.ReleaseManagedResources();
            
            this._isDisposed = true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ChainBuilder()
        {
            this.Dispose(false);
        }
    }
} 