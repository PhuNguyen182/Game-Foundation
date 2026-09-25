using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class AsyncUICommandTests
    {
        private static void WaitUntil(System.Func<bool> condition, int timeoutMs = 2000)
        {
            var sw = Stopwatch.StartNew();
            while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
                Thread.Sleep(1);
        }

        [Test]
        public void Execute_CalledWhileAlreadyExecuting_SecondCallIsDropped()
        {
            var gate = new TaskCompletionSource<bool>();
            int executionCount = 0;
            var command = new AsyncUICommand(ct =>
            {
                executionCount++;
                return new ValueTask(gate.Task);
            });

            command.Execute();
            command.Execute(); // dropped: first execution still in flight

            Assert.That(executionCount, Is.EqualTo(1));

            gate.SetResult(true);
            WaitUntil(() => !command.IsExecuting.CurrentValue);
            command.Dispose();
        }

        [Test]
        public void IsExecuting_TrueWhileRunning_FalseAfterCompletion()
        {
            var gate = new TaskCompletionSource<bool>();
            var command = new AsyncUICommand(ct => new ValueTask(gate.Task));

            command.Execute();
            WaitUntil(() => command.IsExecuting.CurrentValue);
            Assert.That(command.IsExecuting.CurrentValue, Is.True);

            gate.SetResult(true);
            WaitUntil(() => !command.IsExecuting.CurrentValue);

            Assert.That(command.IsExecuting.CurrentValue, Is.False);
            command.Dispose();
        }

        [Test]
        public void Execute_SucceedsAgain_AfterPreviousExecutionCompleted()
        {
            int executionCount = 0;
            var command = new AsyncUICommand(ct =>
            {
                executionCount++;
                return default; // already-completed ValueTask
            });

            command.Execute();
            WaitUntil(() => !command.IsExecuting.CurrentValue);
            command.Execute();
            WaitUntil(() => !command.IsExecuting.CurrentValue);

            Assert.That(executionCount, Is.EqualTo(2));
            command.Dispose();
        }

        [Test]
        public void Dispose_WhileExecuting_CancelsTheInFlightOperation()
        {
            var gate = new TaskCompletionSource<bool>();
            CancellationToken observedToken = default;
            var command = new AsyncUICommand(ct =>
            {
                observedToken = ct;
                return new ValueTask(gate.Task);
            });

            command.Execute();
            WaitUntil(() => command.IsExecuting.CurrentValue);
            command.Dispose();

            Assert.That(observedToken.IsCancellationRequested, Is.True);
        }
    }
}
