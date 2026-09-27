namespace DracoRuan.PrebuildServices.UISystem.Samples.Inventory
{
    public sealed class InventoryItemViewModel
    {
        public InventoryItemViewModel(string itemName, int count)
        {
            this.ItemName = itemName;
            this.Count = count;
        }

        public string ItemName { get; }
        public int Count { get; }
    }
}
