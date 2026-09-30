using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Home
{
    public sealed class HomeScreenView : UIView<HomeScreenViewModel>
    {
        [SerializeField] private UIButton openShopButton;

        protected override void Bind(ref UIBinder binder, HomeScreenViewModel viewModel)
        {
            binder.Command(this.openShopButton, viewModel.OpenShop);
        }
    }
}
