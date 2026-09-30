using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using TMPro;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Inventory
{
    public sealed class InventoryItemWidget : UIWidget<InventoryItemViewModel>
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;

        protected override void Bind(ref UIBinder binder, InventoryItemViewModel viewModel)
        {
            this.nameText.SetText(viewModel.ItemName);
            this.countText.SetText("x" + viewModel.Count);
        }
    }
}
