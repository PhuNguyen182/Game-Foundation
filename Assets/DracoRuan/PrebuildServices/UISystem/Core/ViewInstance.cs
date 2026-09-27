using System;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Logic;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// One open (or cached-but-hidden) view. Non-generic on purpose so heterogeneous views can
    /// share the same stacks/dictionaries; the generic Bind/Unbind call is captured as a
    /// closure at the point where the concrete TVM was still known (see UIService.Open*).
    /// </summary>
    internal sealed class ViewInstance
    {
        public UIViewDefinition Definition;
        public GameObject GameObject;
        public UIViewBase View;
        public UIViewModel ViewModel;
        public UIViewStateMachine StateMachine = new UIViewStateMachine();
        public int SortOrder;
        public UIScope OwningScope;
        public Action UnbindView;
        public Type ViewModelType;

        /// <summary>
        /// Set only for result-yielding opens (OpenForResultAsync/EnqueueAsync); invoked with
        /// the view model itself (typed generically only at the closure's capture site) by
        /// every close path, so Back/backdrop/CloseAll/scope-dispose all resolve the caller's
        /// awaited result — with default(TResult) if Complete() never fired.
        /// </summary>
        public Action<UIViewModel> CompleteResult;

        /// <summary>
        /// Boxed TaskCompletionSource&lt;TResult&gt; for the current result-yielding open, if
        /// any. Reused (not replaced) when ReopenPolicy brings an already-open instance back to
        /// the front instead of spawning a new one, so a second caller awaits the same result
        /// as the first instead of orphaning it.
        /// </summary>
        public object PendingResultCompletionSource;

        /// <summary>Set only while this instance is open, modal, and a focus handler is
        /// attached (UIService.AttachFocusHandler) - disposed on close to stop clamping
        /// EventSystem navigation to this view's root.</summary>
        public IDisposable ModalFocusScope;
    }
}
