using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Inventory
{
    public sealed class InventoryScreenViewModel : UIViewModel
    {
        public InventoryScreenViewModel(IUINavigator navigator) : base(navigator)
        {
        }

        public IReadOnlyList<InventoryItemViewModel> Items { get; private set; }

        protected override void OnActivated(ref DisposableBuilder d)
        {
            var items = new List<InventoryItemViewModel>();
            items.Add(new InventoryItemViewModel("Sword", 1));
            items.Add(new InventoryItemViewModel("Potion", 5));
            items.Add(new InventoryItemViewModel("Gold Coin", 120));
            this.Items = items;
        }
    }
}
