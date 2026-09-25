namespace DracoRuan.PrebuildServices.UISystem.MVVM
{
    /// <summary>
    /// Base helper for view models opened via OpenForResultAsync/EnqueueAsync. Calling
    /// Complete(result) records the result and asks the navigator to close this view; if the
    /// view closes any other way (Back, backdrop, CloseAll), HasResult stays false and the
    /// navigator falls back to default(TResult).
    /// </summary>
    public abstract class ResultViewModel<TArgs, TResult> : UIViewModel<TArgs>, IResultViewModel<TResult>
    {
        protected ResultViewModel(IUINavigator navigator) : base(navigator)
        {
        }

        public bool HasResult { get; private set; }
        public TResult Result { get; private set; }

        public void Complete(TResult result)
        {
            this.Result = result;
            this.HasResult = true;
            this.RequestClose();
        }
    }
}
