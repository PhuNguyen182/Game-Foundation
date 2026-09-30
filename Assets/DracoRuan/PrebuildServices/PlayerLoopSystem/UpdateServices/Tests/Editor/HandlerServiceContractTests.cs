using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices.Tests
{
    /// <summary>
    /// Behavioural contract of the update / fixed-update service managers. Every test runs against both managers.
    /// <para>
    /// Tick order is last-registered-first (CompleteTimerRuntime depends on it). Handlers may register and
    /// deregister anything, including themselves, from inside a tick; none of that may make another handler skip
    /// or double tick.
    /// </para>
    /// </summary>
    [TestFixture(typeof(UpdateDriver))]
    [TestFixture(typeof(FixedUpdateDriver))]
    public sealed class HandlerServiceContractTests<TDriver> where TDriver : ITickDriver, new()
    {
        private TDriver _driver;
        private readonly List<string> _log = new();
        private bool _armed;

        [SetUp]
        public void SetUp()
        {
            this._driver = new TDriver();
            this._driver.Clear();
            this._driver.Tick(); // also lets the manager learn which thread is the main thread
            this._log.Clear();
            this._armed = false;
        }

        [TearDown]
        public void TearDown() => this._driver.Clear();

        /// <summary>Logs its name on every tick and, once <see cref="Activate"/> has run, performs <paramref name="action"/>.</summary>
        private TestHandler Make(string name, Action<TestHandler> action = null)
        {
            var handler = new TestHandler(name);
            handler.OnTick = self =>
            {
                this._log.Add(name);
                if (this._armed)
                    action?.Invoke(self);
            };
            return handler;
        }

        /// <summary>
        /// Registers the handlers and ticks until each has ticked once, then resets counters and the log, so a test
        /// starts from "all handlers are active" no matter how many passes registration takes.
        /// </summary>
        private void Activate(params TestHandler[] handlers)
        {
            this._armed = false;
            foreach (TestHandler handler in handlers)
                this._driver.Register(handler);

            for (int i = 0; i < 3 && handlers.Any(h => h.Ticks == 0); i++)
                this._driver.Tick();

            Assert.That(handlers.All(h => h.Ticks > 0), "Handlers never became active.");
            foreach (TestHandler handler in handlers)
                handler.Ticks = 0;

            this._log.Clear();
            this._armed = true;
        }

        private string Pass()
        {
            this._log.Clear();
            this._driver.Tick();
            return string.Join(",", this._log);
        }

        // ------------------------------------------------------------------ registration

        [Test]
        public void Register_TicksOnTheNextPass_OncePerPass()
        {
            TestHandler a = this.Make("a");

            this._driver.Register(a);
            this._driver.Tick();
            Assert.AreEqual(1, a.Ticks, "A handler registered before a pass must tick in that pass.");

            this._driver.Tick();
            Assert.AreEqual(2, a.Ticks);
        }

        [Test]
        public void RegisterDuringTick_DoesNotTickInTheSamePass()
        {
            TestHandler late = this.Make("late");
            TestHandler early = this.Make("early", _ => this._driver.Register(late));
            this.Activate(early);

            Assert.AreEqual("early", this.Pass());
            Assert.AreEqual(0, late.Ticks, "A handler registered mid-pass must wait for the next pass.");

            Assert.AreEqual("late,early", this.Pass());
        }

        [Test]
        public void Order_IsLastRegisteredFirst()
        {
            TestHandler a = this.Make("a"), b = this.Make("b"), c = this.Make("c");
            this.Activate(a, b, c);

            Assert.AreEqual("c,b,a", this.Pass());
        }

        [Test]
        public void Register_SameHandlerTwice_TicksOnce()
        {
            TestHandler x = this.Make("x");
            this._driver.Register(x);
            this._driver.Register(x);
            this.Activate(x);

            Assert.AreEqual("x", this.Pass());
        }

        [Test]
        public void Register_AlreadyActiveHandler_TicksOncePerPass()
        {
            TestHandler x = this.Make("x");
            this.Activate(x);

            this._driver.Register(x);

            Assert.AreEqual("x", this.Pass());
            Assert.AreEqual("x", this.Pass());
        }

        [Test]
        public void RegisterThenDeregister_InTheSameFrame_NeverTicks()
        {
            TestHandler x = this.Make("x");

            this._driver.Register(x);
            this._driver.Deregister(x);
            this._driver.Tick();
            this._driver.Tick();

            Assert.AreEqual(0, x.Ticks, "Handler left a zombie entry behind.");
        }

        [Test]
        public void RegisterDeregisterRegister_InTheSameFrame_TicksOncePerPass()
        {
            // Object-pool pattern: despawn and respawn within a single frame.
            TestHandler x = this.Make("x");

            this._driver.Register(x);
            this._driver.Deregister(x);
            this._driver.Register(x);
            this.Activate(x);

            Assert.AreEqual("x", this.Pass());
        }

        [Test]
        public void Register_Null_IsIgnored()
        {
            this._driver.Register(null);

            Assert.DoesNotThrow(() => this._driver.Tick());
            Assert.DoesNotThrow(() => this._driver.Tick());
        }

        [Test]
        public void Deregister_Null_IsIgnored()
        {
            TestHandler a = this.Make("a");
            this.Activate(a);

            Assert.DoesNotThrow(() => this._driver.Deregister(null));
            Assert.AreEqual("a", this.Pass());
        }

        // ------------------------------------------------------------------ deregistration

        [Test]
        public void Deregister_StopsTicking()
        {
            TestHandler a = this.Make("a"), b = this.Make("b");
            this.Activate(a, b);

            this._driver.Deregister(a);

            Assert.AreEqual("b", this.Pass());
        }

        [Test]
        public void Deregister_NeverRegisteredHandler_IsANoOp()
        {
            TestHandler a = this.Make("a");
            this.Activate(a);

            this._driver.Deregister(this.Make("stranger"));

            Assert.AreEqual("a", this.Pass());
        }

        [Test]
        public void SelfDeregisterDuringTick_DoesNotSkipTheNextHandler()
        {
            TestHandler a = this.Make("a");
            TestHandler b = this.Make("b", self => this._driver.Deregister(self));
            TestHandler c = this.Make("c");
            this.Activate(a, b, c);

            Assert.AreEqual("c,b,a", this.Pass(), "b removing itself made a skip this frame.");
            Assert.AreEqual("c,a", this.Pass());
        }

        [Test]
        public void SelfDeregisterTwiceDuringTick_DoesNotSkipAnyone()
        {
            TestHandler a = this.Make("a");
            TestHandler b = this.Make("b");
            TestHandler c = this.Make("c", self =>
            {
                this._driver.Deregister(self);
                this._driver.Deregister(self);
            });
            this.Activate(a, b, c);

            Assert.AreEqual("c,b,a", this.Pass());
            Assert.AreEqual("b,a", this.Pass());
        }

        [Test]
        public void DeregisterAlreadyTickedHandlerDuringTick_DoesNotSkipAnyone()
        {
            TestHandler a = this.Make("a");
            TestHandler c = this.Make("c");
            TestHandler b = this.Make("b", _ => this._driver.Deregister(c)); // c already ticked this pass
            this.Activate(a, b, c);

            Assert.AreEqual("c,b,a", this.Pass());
            Assert.AreEqual("b,a", this.Pass());
        }

        [Test]
        public void DeregisterNotYetTickedHandlerDuringTick_PreventsItsTick_AndSkipsNoOtherHandler()
        {
            TestHandler a = this.Make("a");
            TestHandler b = this.Make("b");
            TestHandler c = this.Make("c", _ => this._driver.Deregister(a)); // a would tick last
            this.Activate(a, b, c);

            Assert.AreEqual("c,b", this.Pass());
            Assert.AreEqual("c,b", this.Pass());
        }

        [Test]
        public void DeregisterUnknownHandlerDuringTick_DoesNotSkipAnyone()
        {
            TestHandler stranger = this.Make("stranger");
            TestHandler a = this.Make("a");
            TestHandler b = this.Make("b");
            TestHandler c = this.Make("c", _ => this._driver.Deregister(stranger));
            this.Activate(a, b, c);

            Assert.AreEqual("c,b,a", this.Pass());
        }

        [Test]
        public void RegisterThenDeregisterInsideATick_NeverTicksTheHandler()
        {
            TestHandler victim = this.Make("victim");
            TestHandler spawner = this.Make("spawner", _ =>
            {
                this._driver.Register(victim);
                this._driver.Deregister(victim);
            });
            this.Activate(spawner);

            Assert.AreEqual("spawner", this.Pass());
            Assert.AreEqual("spawner", this.Pass());
            Assert.AreEqual(0, victim.Ticks);
        }

        [Test]
        public void ManyRemovals_KeepRemainingHandlersCorrectlyTracked()
        {
            const int total = 100;
            var handlers = new TestHandler[total];
            for (int i = 0; i < total; i++)
                handlers[i] = this.Make("h" + i);
            this.Activate(handlers);

            // Remove 80 of 100 - enough to force internal compaction - then remove the survivors by identity.
            var survivors = new List<TestHandler>();
            for (int i = 0; i < total; i++)
            {
                if (i % 5 == 0)
                    survivors.Add(handlers[i]);
                else
                    this._driver.Deregister(handlers[i]);
            }

            string expected = string.Join(",",
                Enumerable.Range(0, total).Where(i => i % 5 == 0).Reverse().Select(i => "h" + i));
            Assert.AreEqual(expected, this.Pass());
            Assert.AreEqual(expected, this.Pass());

            foreach (TestHandler survivor in survivors.AsEnumerable().Reverse())
            {
                this._driver.Deregister(survivor);
                this._driver.Tick();
            }

            Assert.AreEqual("", this.Pass());

            foreach (TestHandler handler in handlers)
                this._driver.Register(handler);
            this._driver.Tick();
            Assert.AreEqual(total, this.Pass().Split(',').Length);
        }

        // ------------------------------------------------------------------ Clear

        [Test]
        public void Clear_RemovesActiveAndPendingHandlers_AndAllowsReRegistering()
        {
            TestHandler active = this.Make("active");
            TestHandler pending = this.Make("pending");
            this.Activate(active);
            this._driver.Register(pending);

            this._driver.Clear();

            Assert.AreEqual("", this.Pass());
            Assert.AreEqual("", this.Pass());
            Assert.AreEqual(0, pending.Ticks);

            this._driver.Register(active);
            Assert.AreEqual("active", this.Pass());
        }

        [Test]
        public void ClearDuringTick_StopsFurtherTicks_WithoutThrowing()
        {
            TestHandler a = this.Make("a");
            TestHandler b = this.Make("b", _ => this._driver.Clear());
            TestHandler c = this.Make("c");
            this.Activate(a, b, c);

            string firstPass = null;
            Assert.DoesNotThrow(() => firstPass = this.Pass());
            Assert.AreEqual("c,b", firstPass);
            Assert.AreEqual("", this.Pass());

            this._driver.Register(a);
            Assert.AreEqual("a", this.Pass());
        }

        // ------------------------------------------------------------------ fault isolation

        [Test]
        public void ThrowingHandler_DoesNotStopOthers_OrBlockNewRegistrations()
        {
            TestHandler late = this.Make("late");
            TestHandler a = this.Make("a", _ => this._driver.Register(late));
            TestHandler b = this.Make("b", _ => throw new InvalidOperationException("boom-b"));
            TestHandler c = this.Make("c");
            this.Activate(a, b, c);

            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("boom-b"));
            string firstPass = null;
            Assert.DoesNotThrow(() => firstPass = this.Pass());
            Assert.AreEqual("c,b,a", firstPass, "A throwing handler must not stop the rest of the pass.");

            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("boom-b"));
            Assert.AreEqual("late,c,b,a", this.Pass(), "Registrations made during a faulted pass must still go live.");
        }

        [Test]
        public void EveryThrowingHandlerInAPass_IsLogged_AndAllOthersStillTick()
        {
            TestHandler a = this.Make("a");
            TestHandler b = this.Make("b", _ => throw new InvalidOperationException("boom-b"));
            TestHandler c = this.Make("c", _ => throw new InvalidOperationException("boom-c"));
            TestHandler d = this.Make("d");
            TestHandler e = this.Make("e", _ => throw new InvalidOperationException("boom-e"));
            TestHandler f = this.Make("f", _ => throw new InvalidOperationException("boom-f"));
            this.Activate(a, b, c, d, e, f);

            // Ticks run f, e, d, c, b, a; consecutive throwers (f, e) and the last-but-one (b) must all be survived.
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("boom-f"));
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("boom-e"));
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("boom-c"));
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("boom-b"));
            Assert.AreEqual("f,e,d,c,b,a", this.Pass());
        }

        // ------------------------------------------------------------------ misuse

        [Test]
        public void ReentrantTick_IsRejectedWithAnError_AndDoesNotDoubleTick()
        {
            bool reentered = false;
            TestHandler a = this.Make("a", _ =>
            {
                if (reentered)
                    return;
                reentered = true;
                this._driver.Tick();
            });
            this.Activate(a);

            LogAssert.Expect(UnityEngine.LogType.Error, new Regex("(?i)re-?entran"));
            Assert.AreEqual("a", this.Pass());
        }

        [Test]
        public void RegisterFromAnotherThread_IsRejected_AndDoesNotRegister()
        {
            TestHandler x = this.Make("x");
            Exception thrown = null;

            var worker = new Thread(() =>
            {
                try
                {
                    this._driver.Register(x);
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            });
            worker.Start();
            worker.Join();

            Assert.IsInstanceOf<InvalidOperationException>(thrown, "Register must refuse calls off the main thread.");
            this._driver.Tick();
            this._driver.Tick();
            Assert.AreEqual(0, x.Ticks);
        }

        [Test]
        public void DeregisterFromAnotherThread_IsRejected_AndLeavesTheHandlerRegistered()
        {
            TestHandler x = this.Make("x");
            this.Activate(x);
            Exception thrown = null;

            var worker = new Thread(() =>
            {
                try
                {
                    this._driver.Deregister(x);
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            });
            worker.Start();
            worker.Join();

            Assert.IsInstanceOf<InvalidOperationException>(thrown, "Deregister must refuse calls off the main thread.");
            Assert.AreEqual("x", this.Pass());
        }
    }
}