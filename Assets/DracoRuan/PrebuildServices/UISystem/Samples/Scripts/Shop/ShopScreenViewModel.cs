using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.MVVM.DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.MVVM.DracoRuan.PrebuildServices.UISystem.MVVM.Commands;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Shop
{
    public sealed class ShopScreenViewModel : UIViewModel
    {
        public ShopScreenViewModel(IUINavigator navigator) : base(navigator)
        {
        }

        public UICommand Back { get; private set; }

        protected override void OnActivated(ref DisposableBuilder d)
        {
            this.Back = new UICommand(this.OnBack);
            d.Add(this.Back);
        }

        private void OnBack()
        {
            _ = this.Navigator.PopScreenAsync();
        }
    }
}
