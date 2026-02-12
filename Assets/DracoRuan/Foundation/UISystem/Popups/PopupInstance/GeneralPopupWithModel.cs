using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.UISystem.Views;

namespace DracoRuan.Foundation.UISystem.Popups.PopupInstance
{
    public abstract class GeneralPopupWithModel<TModel> : BaseUIPopup, IUIModel<TModel>
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
    }
}
