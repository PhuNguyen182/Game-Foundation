using System.Threading;
using System.Threading.Tasks;

namespace DracoRuan.PrebuildServices.UISystem.MVVM
{
    /// <summary>
    /// Navigation contract available to view models. Code and view models only ever talk to
    /// this interface (by view-model type), never to a view/MonoBehaviour directly.
    /// </summary>
    public interface IUINavigator
    {
        ValueTask OpenAsync<TViewModel>(CancellationToken ct = default)
            where TViewModel : UIViewModel;

        ValueTask OpenAsync<TViewModel, TArgs>(TArgs args, CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>;

        ValueTask<TResult> OpenForResultAsync<TViewModel, TArgs, TResult>(TArgs args, CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult>;

        ValueTask<TResult> EnqueueAsync<TViewModel, TArgs, TResult>(TArgs args, int priority = 0, CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult>;

        ValueTask CloseAsync<TViewModel>() where TViewModel : UIViewModel;

        ValueTask CloseAsync(UIViewModel viewModel);

        ValueTask PopScreenAsync();

        ValueTask PopToRootAsync();
    }
}
