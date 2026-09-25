using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.MVVM;

namespace DracoRuan.PrebuildServices.UISystem.Testing
{
    /// <summary>
    /// Records every navigation call so a view model's behavior can be asserted without a
    /// real router/view/prefab. Set <see cref="NextResult"/> before triggering a call that
    /// will hit OpenForResultAsync/EnqueueAsync to control what it returns.
    /// </summary>
    public sealed class FakeUINavigator : IUINavigator
    {
        public List<string> Calls { get; } = new List<string>();

        /// <summary>Boxed value returned by the next OpenForResultAsync/EnqueueAsync call.</summary>
        public object NextResult { get; set; }

        public ValueTask OpenAsync<TViewModel>(CancellationToken ct = default)
            where TViewModel : UIViewModel
        {
            this.Calls.Add($"Open<{typeof(TViewModel).Name}>()");
            return default;
        }

        public ValueTask OpenAsync<TViewModel, TArgs>(TArgs args, CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>
        {
            this.Calls.Add($"Open<{typeof(TViewModel).Name}>({args})");
            return default;
        }

        public ValueTask<TResult> OpenForResultAsync<TViewModel, TArgs, TResult>(TArgs args, CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult>
        {
            this.Calls.Add($"OpenForResult<{typeof(TViewModel).Name}>({args})");
            return new ValueTask<TResult>((TResult)this.NextResult);
        }

        public ValueTask<TResult> EnqueueAsync<TViewModel, TArgs, TResult>(TArgs args, int priority = 0, CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult>
        {
            this.Calls.Add($"Enqueue<{typeof(TViewModel).Name}>({args}, priority={priority})");
            return new ValueTask<TResult>((TResult)this.NextResult);
        }

        public ValueTask CloseAsync<TViewModel>() where TViewModel : UIViewModel
        {
            this.Calls.Add($"Close<{typeof(TViewModel).Name}>()");
            return default;
        }

        public ValueTask CloseAsync(UIViewModel viewModel)
        {
            this.Calls.Add($"Close({viewModel.GetType().Name})");
            return default;
        }

        public ValueTask PopScreenAsync()
        {
            this.Calls.Add("PopScreen()");
            return default;
        }

        public ValueTask PopToRootAsync()
        {
            this.Calls.Add("PopToRoot()");
            return default;
        }
    }
}
