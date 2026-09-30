using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using TMPro;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Toast
{
    public sealed class ToastView : UIView<ToastViewModel>
    {
        [SerializeField] private TMP_Text messageText;

        protected override void Bind(ref UIBinder binder, ToastViewModel viewModel)
        {
            this.messageText.SetText(viewModel.Message);
        }
    }
}
