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

        /// <summary>Set only for result-yielding opens; completed by UIService.CloseAsync.</summary>
        public Action<object> CompleteResult;
    }
}
