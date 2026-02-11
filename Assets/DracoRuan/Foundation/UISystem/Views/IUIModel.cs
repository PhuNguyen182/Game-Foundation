using System;

namespace DracoRuan.Foundation.UISystem.Views
{
    public interface IUIModel<TModel>
    {
        public TModel Model { get; }

        public event Action OnModelUpdated;

        public void BindModelData(TModel model);
        public void UnbindModelData();
        public void OnModelDataBound(TModel model);
        public void OnModelDataUnbound();
    }
}