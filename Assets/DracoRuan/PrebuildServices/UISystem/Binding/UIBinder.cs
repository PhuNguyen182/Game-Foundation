using System;
using DracoRuan.PrebuildServices.UISystem.Components;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.Views;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Binding
{
    /// <summary>
    /// Collects one Bind() call's worth of subscriptions. A ref struct wrapping an R3
    /// DisposableBuilder (no CompositeDisposable, no class allocation for the binder itself).
    /// After Bind returns, the framework calls Build() once for a single IDisposable to
    /// dispose on Unbind.
    /// </summary>
    public ref struct UIBinder
    {
        private DisposableBuilder _builder;

        public IDisposable Build() => this._builder.Build();

        /// <summary>Registers a disposable to run on Unbind. Exposed so extension methods
        /// outside this file (e.g. UIBinderTweenExtensions) can participate in the same
        /// per-Bind() disposal without reaching into the private _builder field.</summary>
        public void Add(IDisposable disposable) => disposable.AddTo(ref this._builder);

        // ---- One-way ----

        public void Text(TMP_Text target, Observable<string> source) =>
            source.SubscribeToText(target).AddTo(ref this._builder);

        public void Text<T>(TMP_Text target, Observable<T> source, Func<T, string> selector) =>
            source.SubscribeToText(target, selector).AddTo(ref this._builder);

        public void Active(GameObject target, Observable<bool> source) =>
            source.Subscribe(target, static (value, t) => t.SetActive(value)).AddTo(ref this._builder);

        public void Active(Component target, Observable<bool> source) =>
            this.Active(target.gameObject, source);

        public void Interactable(Selectable target, Observable<bool> source) =>
            source.Subscribe(target, static (value, t) => t.interactable = value).AddTo(ref this._builder);

        public void Fill(Image target, Observable<float> source) =>
            source.Subscribe(target, static (value, t) => t.fillAmount = value).AddTo(ref this._builder);

        public void Sprite(Image target, Observable<Sprite> source) =>
            source.Subscribe(target, static (value, t) => t.sprite = value).AddTo(ref this._builder);

        public void Color(Graphic target, Observable<Color> source) =>
            source.Subscribe(target, static (value, t) => t.color = value).AddTo(ref this._builder);

        // ---- Two-way ----

        public void TwoWay(Slider target, ReactiveProperty<float> source)
        {
            source.Subscribe(target, static (value, t) => t.SetValueWithoutNotify(value)).AddTo(ref this._builder);
            target.OnValueChangedAsObservable().Subscribe(source, static (value, s) => s.Value = value)
                .AddTo(ref this._builder);
        }

        public void TwoWay(Toggle target, ReactiveProperty<bool> source)
        {
            source.Subscribe(target, static (value, t) => t.SetIsOnWithoutNotify(value)).AddTo(ref this._builder);
            target.OnValueChangedAsObservable().Subscribe(source, static (value, s) => s.Value = value)
                .AddTo(ref this._builder);
        }

        public void TwoWay(TMP_InputField target, ReactiveProperty<string> source)
        {
            source.Subscribe(target, static (value, t) => t.SetTextWithoutNotify(value)).AddTo(ref this._builder);
            target.OnValueChangedAsObservable().Subscribe(source, static (value, s) => s.Value = value)
                .AddTo(ref this._builder);
        }

        // ---- Command ----

        public void Command(Button target, UICommand command)
        {
            target.OnClickAsObservable().Subscribe(command, static (_, c) => c.Execute()).AddTo(ref this._builder);
            command.CanExecute.Subscribe(target, static (value, t) => t.interactable = value).AddTo(ref this._builder);
        }

        public void Command(Button target, AsyncUICommand command)
        {
            target.OnClickAsObservable().Subscribe(command, static (_, c) => c.Execute()).AddTo(ref this._builder);
            command.CanExecute.Subscribe(target, static (value, t) => t.interactable = value).AddTo(ref this._builder);
        }

        public void Command(UIButton target, UICommand command)
        {
            target.Clicked.Subscribe(command, static (_, c) => c.Execute()).AddTo(ref this._builder);
            command.CanExecute.Subscribe(target, static (value, t) => t.Interactable = value).AddTo(ref this._builder);
        }

        public void Command(UIButton target, AsyncUICommand command)
        {
            target.Clicked.Subscribe(command, static (_, c) => c.Execute()).AddTo(ref this._builder);
            command.CanExecute.Subscribe(target, static (value, t) => t.Interactable = value).AddTo(ref this._builder);
        }

        /// <summary>Raw click stream for callers that don't need a full command (no
        /// CanExecute/IsExecuting) - REWRITE_PLAN.md: "BindClick → Observable&lt;Unit&gt; vẫn
        /// giữ, dùng cho trường hợp không cần command".</summary>
        public void BindClick(UIButton target, Action onClick) =>
            target.Clicked.Subscribe(onClick, static (_, a) => a()).AddTo(ref this._builder);

        // ---- Widget (nested, non-routed) ----

        public void Widget<TChildViewModel>(UIWidget<TChildViewModel> widget, TChildViewModel viewModel)
            where TChildViewModel : class
        {
            widget.BindViewModel(viewModel);
            Disposable.Create(widget, static w => w.UnbindViewModel()).AddTo(ref this._builder);
        }

        // ---- Escape hatch ----

        public void Custom<T>(Observable<T> source, Action<T> action) =>
            source.Subscribe(action).AddTo(ref this._builder);
    }
}