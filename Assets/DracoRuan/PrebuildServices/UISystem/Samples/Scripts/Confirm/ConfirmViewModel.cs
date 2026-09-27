using DracoRuan.PrebuildServices.UISystem.MVVM;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Confirm
{
    /// <summary>REWRITE_PLAN.md mục 5 bước 9 sample: a yes/no popup opened via
    /// OpenForResultAsync&lt;ConfirmViewModel, ConfirmArgs, bool&gt;(args) - the caller awaits
    /// true/false directly, no event/callback wiring.</summary>
    public sealed class ConfirmViewModel : ResultViewModel<ConfirmArgs, bool>
    {
        private ConfirmArgs _args;

        public ConfirmViewModel(IUINavigator navigator) : base(navigator)
        {
        }

        public string Title => this._args.Title;
        public string Message => this._args.Message;

        public UICommand Confirm { get; private set; }
        public UICommand Cancel { get; private set; }

        protected override void OnActivated(ConfirmArgs args, ref DisposableBuilder d)
        {
            this._args = args;
            this.Confirm = new UICommand(() => this.Complete(true));
            this.Cancel = new UICommand(() => this.Complete(false));
            d.Add(this.Confirm);
            d.Add(this.Cancel);
        }
    }
}
