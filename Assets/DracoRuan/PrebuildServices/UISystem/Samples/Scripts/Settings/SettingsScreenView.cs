using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Settings
{
    public sealed class SettingsScreenView : UIView<SettingsScreenViewModel>
    {
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Slider volumeSlider;

        protected override void Bind(ref UIBinder binder, SettingsScreenViewModel viewModel)
        {
            binder.TwoWay(this.musicToggle, viewModel.MusicEnabled);
            binder.TwoWay(this.volumeSlider, viewModel.Volume);
        }
    }
}
