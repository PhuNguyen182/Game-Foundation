using System;
using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core;
using NUnit.Framework;
using UnityEngine.LowLevel;
using LoopSystem = UnityEngine.LowLevel.PlayerLoopSystem;
using UnityEngine.PlayerLoop;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices.Tests
{
    /// <summary>
    /// Installing and removing the service managers' player-loop systems. Each test starts from Unity's default loop and
    /// puts the real one back afterwards.
    /// </summary>
    [TestFixture]
    public sealed class PlayerLoopBootstrapperTests
    {
        private LoopSystem _originalLoop;

        [SetUp]
        public void SetUp()
        {
            this._originalLoop = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
            UpdateServiceManager.Clear();
            FixedUpdateServiceManager.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerLoop.SetPlayerLoop(this._originalLoop);
            UpdateServiceManager.Clear();
            FixedUpdateServiceManager.Clear();
        }

        /// <summary>Managed systems are the ones with an update delegate; Unity's own entries only have a native function.</summary>
        private static List<LoopSystem> ManagedSystems()
        {
            var found = new List<LoopSystem>();
            Collect(PlayerLoop.GetCurrentPlayerLoop(), found);
            return found;

            static void Collect(LoopSystem system, List<LoopSystem> into)
            {
                if (system.updateDelegate != null)
                    into.Add(system);

                if (system.subSystemList == null)
                    return;

                foreach (LoopSystem child in system.subSystemList)
                    Collect(child, into);
            }
        }

        private static LoopSystem Find(LoopSystem root, Type type)
        {
            if (root.type == type)
                return root;

            if (root.subSystemList != null)
            {
                foreach (LoopSystem child in root.subSystemList)
                {
                    LoopSystem match = Find(child, type);
                    if (match.type != null)
                        return match;
                }
            }

            return default;
        }

        private static int IndexOf(LoopSystem parent, Func<LoopSystem, bool> predicate)
        {
            for (int i = 0; i < parent.subSystemList.Length; i++)
            {
                if (predicate(parent.subSystemList[i]))
                    return i;
            }

            return -1;
        }

        [Test]
        public void Initialize_InstallsOneUpdateAndOneFixedUpdateSystem()
        {
            PlayerLoopBootstrapper.Initialize();

            Assert.AreEqual(2, ManagedSystems().Count);
        }

        [Test]
        public void Initialize_CalledAgain_DoesNotInstallDuplicates()
        {
            // Enter Play Mode without domain reload keeps the previous session's systems in the loop.
            PlayerLoopBootstrapper.Initialize();
            PlayerLoopBootstrapper.Initialize();
            PlayerLoopBootstrapper.Initialize();

            Assert.AreEqual(2, ManagedSystems().Count, "Every Initialize call added another pair of systems.");
        }

        [Test]
        public void InstalledSystems_TickTheManagers_OncePerInvocation()
        {
            var update = new TestHandler("update");
            var fixedUpdate = new TestHandler("fixed");
            UpdateServiceManager.RegisterUpdateHandler(update);
            FixedUpdateServiceManager.RegisterFixedUpdateHandler(fixedUpdate);

            PlayerLoopBootstrapper.Initialize();
            List<LoopSystem> systems = ManagedSystems();
            foreach (LoopSystem system in systems)
                system.updateDelegate();
            foreach (LoopSystem system in systems)
                system.updateDelegate();

            // Each manager ticks only its own handler: two invocations of the pair -> two ticks each.
            Assert.AreEqual(2, update.Ticks);
            Assert.AreEqual(2, fixedUpdate.Ticks);
        }

        [Test]
        public void UpdateSystem_RunsBeforeUserScripts()
        {
            PlayerLoopBootstrapper.Initialize();

            LoopSystem update = Find(PlayerLoop.GetCurrentPlayerLoop(), typeof(Update));
            int ours = IndexOf(update, s => s.updateDelegate != null);
            int userScripts = IndexOf(update, s => s.type == typeof(Update.ScriptRunBehaviourUpdate));

            Assert.That(ours, Is.GreaterThanOrEqualTo(0), "System not found inside Update.");
            Assert.That(userScripts, Is.GreaterThanOrEqualTo(0));
            Assert.That(ours, Is.LessThan(userScripts));
        }

        [Test]
        public void FixedUpdateSystem_RunsBeforeUserScripts()
        {
            PlayerLoopBootstrapper.Initialize();

            LoopSystem fixedUpdate = Find(PlayerLoop.GetCurrentPlayerLoop(), typeof(FixedUpdate));
            int ours = IndexOf(fixedUpdate, s => s.updateDelegate != null);
            int userScripts = IndexOf(fixedUpdate, s => s.type == typeof(FixedUpdate.ScriptRunBehaviourFixedUpdate));

            Assert.That(ours, Is.GreaterThanOrEqualTo(0), "System not found inside FixedUpdate.");
            Assert.That(userScripts, Is.GreaterThanOrEqualTo(0));
            Assert.That(ours, Is.LessThan(userScripts));
        }

        [Test]
        public void Initialize_LeavesUnitysOwnSystemsInPlace()
        {
            int nativeBefore = CountAll(PlayerLoop.GetCurrentPlayerLoop());

            PlayerLoopBootstrapper.Initialize();

            Assert.AreEqual(nativeBefore + 2, CountAll(PlayerLoop.GetCurrentPlayerLoop()));

            static int CountAll(LoopSystem system) =>
                1 + (system.subSystemList?.Sum(CountAll) ?? 0);
        }

        [Test]
        public void Shutdown_RemovesTheSystemsAndClearsTheManagers()
        {
            var update = new TestHandler("update");
            var fixedUpdate = new TestHandler("fixed");
            PlayerLoopBootstrapper.Initialize();
            UpdateServiceManager.RegisterUpdateHandler(update);
            FixedUpdateServiceManager.RegisterFixedUpdateHandler(fixedUpdate);

            PlayerLoopBootstrapper.Shutdown();

            Assert.AreEqual(0, ManagedSystems().Count, "Shutdown left systems in the player loop.");

            UpdateServiceManager.UpdateTime();
            UpdateServiceManager.UpdateTime();
            FixedUpdateServiceManager.FixedUpdateTime();
            FixedUpdateServiceManager.FixedUpdateTime();
            Assert.AreEqual(0, update.Ticks);
            Assert.AreEqual(0, fixedUpdate.Ticks);
        }

        [Test]
        public void Shutdown_AfterRepeatedInitialize_RemovesEverything()
        {
            PlayerLoopBootstrapper.Initialize();
            PlayerLoopBootstrapper.Initialize();

            PlayerLoopBootstrapper.Shutdown();

            Assert.AreEqual(0, ManagedSystems().Count);
        }

        [Test]
        public void Shutdown_WithoutInitialize_IsHarmless()
        {
            Assert.DoesNotThrow(PlayerLoopBootstrapper.Shutdown);
            Assert.AreEqual(0, ManagedSystems().Count);
        }

        [Test]
        public void Initialize_AfterShutdown_InstallsAgain()
        {
            PlayerLoopBootstrapper.Initialize();
            PlayerLoopBootstrapper.Shutdown();

            PlayerLoopBootstrapper.Initialize();

            Assert.AreEqual(2, ManagedSystems().Count);
        }
    }
}
