using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.Testing;
using NUnit.Framework;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class UIViewModelTests
    {
        private sealed class CountingViewModel : UIViewModel
        {
            // Subject, not ReactiveProperty: a plain event stream with no replay-on-subscribe,
            // so subscription counts below reflect only values pushed after subscribing.
            private readonly Subject<int> _source = new Subject<int>();
            public int ObservedCount;
            public int DeactivatedCount;
            public int DisposedCount;
            public int LateObservedCount;

            public CountingViewModel(IUINavigator navigator) : base(navigator) { }

            protected override void OnActivated(ref DisposableBuilder d)
            {
                this._source.Subscribe(_ => this.ObservedCount++).AddTo(ref d);
            }

            protected override void OnDeactivated() => this.DeactivatedCount++;
            protected override void OnDispose() => this.DisposedCount++;

            public void Push(int value) => this._source.OnNext(value);

            public void SubscribeLate(Observable<int> source) =>
                source.Subscribe(_ => this.LateObservedCount++).AddTo(ref this.Late);
        }

        [Test]
        public void Activate_SubscriptionFromOnActivated_ReceivesValues()
        {
            var vm = new CountingViewModel(new FakeUINavigator());
            vm.Activate();

            vm.Push(1);
            vm.Push(2);

            Assert.That(vm.ObservedCount, Is.EqualTo(2));
        }

        [Test]
        public void Deactivate_DisposesActivationSubscriptions_AndCallsOnDeactivated()
        {
            var vm = new CountingViewModel(new FakeUINavigator());
            vm.Activate();
            vm.Push(1);

            vm.Deactivate();
            vm.Push(2); // should no longer be observed

            Assert.That(vm.ObservedCount, Is.EqualTo(1));
            Assert.That(vm.DeactivatedCount, Is.EqualTo(1));
        }

        [Test]
        public void Activate_AfterDeactivate_GetsFreshSubscription_NoDoubleFiring()
        {
            var vm = new CountingViewModel(new FakeUINavigator());
            vm.Activate();
            vm.Deactivate();

            vm.Activate();
            vm.Push(1);

            Assert.That(vm.ObservedCount, Is.EqualTo(1));
        }

        [Test]
        public void Late_SubscriptionsAreDisposed_OnDeactivate()
        {
            var vm = new CountingViewModel(new FakeUINavigator());
            vm.Activate();

            var late = new Subject<int>();
            vm.SubscribeLate(late);

            late.OnNext(1);
            Assert.That(vm.LateObservedCount, Is.EqualTo(1));

            vm.Deactivate();
            late.OnNext(2);

            Assert.That(vm.LateObservedCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_CallsOnDispose_AndIsIdempotent()
        {
            var vm = new CountingViewModel(new FakeUINavigator());
            vm.Activate();

            vm.Dispose();
            vm.Dispose();

            Assert.That(vm.DisposedCount, Is.EqualTo(1));
        }

        [Test]
        public void HandleBack_DefaultImplementation_ReturnsClose()
        {
            var vm = new CountingViewModel(new FakeUINavigator());

            Assert.That(vm.HandleBack(), Is.EqualTo(BackResult.Close));
        }
    }
}
