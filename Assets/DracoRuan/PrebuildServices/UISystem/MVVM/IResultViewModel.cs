namespace DracoRuan.PrebuildServices.UISystem.MVVM
{
    /// <summary>
    /// Implemented by view models opened via IUINavigator.OpenForResultAsync/EnqueueAsync.
    /// HasResult/Result are part of the contract (not just ResultViewModel&lt;,&gt;'s own
    /// properties) so the navigator can read a pending result back through this interface
    /// alone, without reflection or assuming every implementer derives from ResultViewModel.
    /// </summary>
    public interface IResultViewModel<TResult>
    {
        bool HasResult { get; }
        TResult Result { get; }
        void Complete(TResult result);
    }
}
