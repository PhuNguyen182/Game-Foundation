using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Inventory
{
    public sealed class InventoryScreenView : UIView<InventoryScreenViewModel>
    {
        [SerializeField] private InventoryItemWidget itemWidget0;
        [SerializeField] private InventoryItemWidget itemWidget1;
        [SerializeField] private InventoryItemWidget itemWidget2;

        protected override void Bind(ref UIBinder binder, InventoryScreenViewModel viewModel)
        {
            var items = viewModel.Items;
            if (items.Count > 0) binder.Widget(this.itemWidget0, items[0]);
            if (items.Count > 1) binder.Widget(this.itemWidget1, items[1]);
            if (items.Count > 2) binder.Widget(this.itemWidget2, items[2]);
        }
    }
}
