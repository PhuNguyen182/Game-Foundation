using DracoRuan.PrebuildServices.UISystem.MVVM;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Home
{
    public sealed class HomeScreenViewModel : UIViewModel
    {
        public HomeScreenViewModel(IUINavigator navigator) : base(navigator)
        {
        }

        public UICommand OpenShop { get; private set; }

        protected override void OnActivated(ref DisposableBuilder d)
        {
            this.OpenShop = new UICommand(this.OnOpenShop);
            d.Add(this.OpenShop);
        }

        private void OnOpenShop()
        {
            _ = this.Navigator.OpenAsync<Shop.ShopScreenViewModel>(this.ActivationToken);
        }
    }
}
