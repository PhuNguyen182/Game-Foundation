using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.Testing;
using NUnit.Framework;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class UIViewModelOfArgsTests
    {
        private sealed class GreetViewModel : UIViewModel<string>
        {
            public string ReceivedArgs;

            public GreetViewModel(IUINavigator navigator) : base(navigator) { }

            protected override void OnActivated(string args, ref DisposableBuilder d) => this.ReceivedArgs = args;
        }

        [Test]
        public void SetArgs_ThenActivate_PassesArgsToTypedOnActivated()
        {
            var vm = new GreetViewModel(new FakeUINavigator());
            vm.SetArgs("hello");

            vm.Activate();

            Assert.That(vm.ReceivedArgs, Is.EqualTo("hello"));
        }

        private sealed class BuyConfirmViewModel : ResultViewModel<string, bool>
        {
            public BuyConfirmViewModel(IUINavigator navigator) : base(navigator) { }

            protected override void OnActivated(string args, ref DisposableBuilder d) { }
        }

        [Test]
        public void Complete_SetsResultAndHasResult_AndRequestsClose()
        {
            var navigator = new FakeUINavigator();
            var vm = new BuyConfirmViewModel(navigator);
            vm.SetArgs("Buy sword?");
            vm.Activate();

            vm.Complete(true);

            Assert.That(vm.HasResult, Is.True);
            Assert.That(vm.Result, Is.True);
            Assert.That(navigator.Calls, Has.Some.Contains("Close(BuyConfirmViewModel)"));
        }

        [Test]
        public void HasResult_BeforeComplete_IsFalse()
        {
            var vm = new BuyConfirmViewModel(new FakeUINavigator());

            Assert.That(vm.HasResult, Is.False);
        }
    }
}
