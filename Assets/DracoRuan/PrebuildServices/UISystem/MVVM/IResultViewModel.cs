namespace DracoRuan.PrebuildServices.UISystem.MVVM
{
    /// <summary>Implemented by view models opened via IUINavigator.OpenForResultAsync/EnqueueAsync.</summary>
    public interface IResultViewModel<TResult>
    {
        void Complete(TResult result);
    }
}
