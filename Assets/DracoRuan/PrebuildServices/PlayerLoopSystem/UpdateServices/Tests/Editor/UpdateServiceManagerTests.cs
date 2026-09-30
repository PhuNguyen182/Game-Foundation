using NUnit.Framework;
using UnityEngine;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices.Tests
{
    /// <summary>Behaviour specific to <see cref="UpdateServiceManager"/> (delta time); the rest lives in the contract tests.</summary>
    [TestFixture]
    public sealed class UpdateServiceManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            UpdateServiceManager.Clear();
            UpdateServiceManager.UpdateTime();
        }

        [TearDown]
        public void TearDown() => UpdateServiceManager.Clear();

        [Test]
        public void EveryHandlerInAPass_ReceivesTheSameScaledDeltaTime()
        {
            var handlers = new TestHandler[5];
            for (int i = 0; i < handlers.Length; i++)
            {
                handlers[i] = new TestHandler("h" + i);
                UpdateServiceManager.RegisterUpdateHandler(handlers[i]);
            }

            UpdateServiceManager.UpdateTime();
            UpdateServiceManager.UpdateTime();

            float expected = Time.deltaTime;
            foreach (TestHandler handler in handlers)
                Assert.AreEqual(expected, handler.LastDeltaTime);
        }
    }
}
