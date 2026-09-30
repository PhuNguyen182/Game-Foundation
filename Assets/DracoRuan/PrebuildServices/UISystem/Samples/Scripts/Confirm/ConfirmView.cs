using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using TMPro;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Confirm
{
    public sealed class ConfirmView : UIView<ConfirmViewModel>
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private UIButton confirmButton;
        [SerializeField] private UIButton cancelButton;

        protected override void Bind(ref UIBinder binder, ConfirmViewModel viewModel)
        {
            this.titleText.SetText(viewModel.Title);
            this.messageText.SetText(viewModel.Message);
            binder.Command(this.confirmButton, viewModel.Confirm);
            binder.Command(this.cancelButton, viewModel.Cancel);
        }
    }
}
