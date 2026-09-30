using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.MVVM.DracoRuan.PrebuildServices.UISystem.MVVM;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Settings
{
    public sealed class SettingsScreenViewModel : UIViewModel
    {
        public SettingsScreenViewModel(IUINavigator navigator) : base(navigator)
        {
            this.MusicEnabled = new ReactiveProperty<bool>(true);
            this.Volume = new ReactiveProperty<float>(0.5f);
        }

        public ReactiveProperty<bool> MusicEnabled { get; }
        public ReactiveProperty<float> Volume { get; }

        protected override void OnActivated(ref DisposableBuilder d)
        {
        }

        protected override void OnDispose()
        {
            this.MusicEnabled.Dispose();
            this.Volume.Dispose();
        }
    }
}
