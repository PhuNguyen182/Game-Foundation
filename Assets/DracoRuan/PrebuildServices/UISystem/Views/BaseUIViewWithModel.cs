using System;

namespace DracoRuan.Foundation.UISystem.Views
{
    public abstract class BaseUIView<TModel> : BaseUIView, IUIModel<TModel>
    {
        private event Action OnModelUpdatedInternal; 
        
        public TModel Model { get; private set; }
        
        public event Action OnModelUpdated
        {
            add => this.OnModelUpdatedInternal += value;
            remove => this.OnModelUpdatedInternal -= value;
        }
        
        public void BindModelData(TModel model)
        {
            this.Model = model;
            this.OnModelDataBound(model);
            this.OnModelUpdatedInternal?.Invoke();
        }

        public void UnbindModelData()
        {
            this.Model = default;
            this.OnModelDataUnbound();
            this.OnModelUpdatedInternal?.Invoke();
        }

        public abstract void OnModelDataBound(TModel model);

        public abstract void OnModelDataUnbound();

        protected virtual void OnDestroy()
        {
            this.UnbindModelData();
            this.OnModelUpdatedInternal = null;
        }
    }
}
