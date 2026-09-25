using DracoRuan.PrebuildServices.UISystem.MVVM;
using NUnit.Framework;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class UICommandTests
    {
        [Test]
        public void Execute_WithNoCanExecuteSource_AlwaysRuns()
        {
            int calls = 0;
            var command = new UICommand(() => calls++);

            command.Execute();
            command.Execute();

            Assert.That(calls, Is.EqualTo(2));
            Assert.That(command.CanExecute.CurrentValue, Is.True);
        }

        [Test]
        public void Execute_WhileCanExecuteIsFalse_DoesNotRun()
        {
            var gate = new ReactiveProperty<bool>(false);
            int calls = 0;
            var command = new UICommand(() => calls++, gate);

            command.Execute();

            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Execute_AfterCanExecuteBecomesTrue_Runs()
        {
            var gate = new ReactiveProperty<bool>(false);
            int calls = 0;
            var command = new UICommand(() => calls++, gate);

            gate.Value = true;
            command.Execute();

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void GenericCommand_PassesParameterThrough()
        {
            string received = null;
            var command = new UICommand<string>(value => received = value);

            command.Execute("sword");

            Assert.That(received, Is.EqualTo("sword"));
        }
    }
}
