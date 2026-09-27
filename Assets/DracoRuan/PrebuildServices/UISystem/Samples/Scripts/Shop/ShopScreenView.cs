using DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.Components;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Shop
{
    public sealed class ShopScreenView : UIView<ShopScreenViewModel>
    {
        [SerializeField] private UIButton backButton;

        protected override void Bind(ref UIBinder binder, ShopScreenViewModel viewModel)
        {
            binder.Command(this.backButton, viewModel.Back);
        }
    }
}
