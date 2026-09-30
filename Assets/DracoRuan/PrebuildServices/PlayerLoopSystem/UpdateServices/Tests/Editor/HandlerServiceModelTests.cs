using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices.Tests
{
    /// <summary>
    /// Randomised differential test: a long series of register / deregister / clear calls, both between passes and
    /// from inside ticking handlers, is applied to the real manager and to a deliberately naive list-based model.
    /// Every pass must tick exactly the handlers the model says, in exactly the order the model says.
    /// <para>
    /// Model semantics: pending handlers join the active list at the start of a pass, ticking is last-in-first-out
    /// over a snapshot, a handler removed before its turn does not tick, a handler registered during a pass waits for
    /// the next one, duplicate and null registrations are ignored.
    /// </para>
    /// </summary>
    [TestFixture(typeof(UpdateDriver))]
    [TestFixture(typeof(FixedUpdateDriver))]
    public sealed class HandlerServiceModelTests<TDriver> where TDriver : ITickDriver, new()
    {
        private const int Passes = 40;

        private enum Kind
        {
            None,
            DeregisterSelf,
            DeregisterOther,
            RegisterOther,
            RegisterSelf,
            DeregisterStranger,
            ClearAll,
        }

        private readonly struct Plan
        {
            public readonly Kind Kind;
            public readonly int Target;

            public Plan(Kind kind, int target)
            {
                this.Kind = kind;
                this.Target = target;
            }
        }

        private interface IOps
        {
            void Register(TestHandler handler);
            void Deregister(TestHandler handler);
            void Clear();
        }

        private sealed class DriverOps : IOps
        {
            private readonly ITickDriver _driver;
            public DriverOps(ITickDriver driver) => this._driver = driver;
            public void Register(TestHandler handler) => this._driver.Register(handler);
            public void Deregister(TestHandler handler) => this._driver.Deregister(handler);
            public void Clear() => this._driver.Clear();
        }

        private sealed class Model : IOps
        {
            public readonly List<TestHandler> Active = new();
            public readonly List<TestHandler> Pending = new();

            public void Register(TestHandler handler)
            {
                if (handler == null || this.Active.Contains(handler) || this.Pending.Contains(handler))
                    return;
                this.Pending.Add(handler);
            }

            public void Deregister(TestHandler handler)
            {
                if (!this.Active.Remove(handler))
                    this.Pending.Remove(handler);
            }

            public void Clear()
            {
                this.Active.Clear();
                this.Pending.Clear();
            }
        }

        private static void Apply(IOps ops, Plan plan, TestHandler self, TestHandler[] all, TestHandler stranger)
        {
            switch (plan.Kind)
            {
                case Kind.DeregisterSelf: ops.Deregister(self); break;
                case Kind.DeregisterOther: ops.Deregister(all[plan.Target]); break;
                case Kind.RegisterOther: ops.Register(all[plan.Target]); break;
                case Kind.RegisterSelf: ops.Register(self); break;
                case Kind.DeregisterStranger: ops.Deregister(stranger); break;
                case Kind.ClearAll: ops.Clear(); break;
            }
        }

        private static Plan RandomPlan(System.Random rng, int handlerCount)
        {
            int roll = rng.Next(100);
            if (roll < 55) return new Plan(Kind.None, 0);
            if (roll < 70) return new Plan(Kind.DeregisterSelf, 0);
            if (roll < 80) return new Plan(Kind.DeregisterOther, rng.Next(handlerCount));
            if (roll < 90) return new Plan(Kind.RegisterOther, rng.Next(handlerCount));
            if (roll < 94) return new Plan(Kind.RegisterSelf, 0);
            if (roll < 98) return new Plan(Kind.DeregisterStranger, 0);
            return new Plan(Kind.ClearAll, 0);
        }

        [SetUp]
        public void SetUp()
        {
            new TDriver().Clear();
            new TDriver().Tick();
        }

        [TearDown]
        public void TearDown() => new TDriver().Clear();

        [Test]
        public void RandomOperations_MatchTheReferenceModel(
            [Range(1, 25)] int seed,
            [Values(6, 40, 300)] int handlerCount) => RunRandomOperations(seed, handlerCount, Passes);

        [Test]
        public void RandomOperations_BeyondTheInitialCapacity_MatchTheReferenceModel([Range(1, 3)] int seed) =>
            // 3,000 handlers outgrow the 1,000-slot initial capacity, exercising growth and index rebuilds.
            RunRandomOperations(seed, 3000, 12);

        private static void RunRandomOperations(int seed, int handlerCount, int passes)
        {
            var driver = new TDriver();
            var driverOps = new DriverOps(driver);
            var model = new Model();
            var rng = new System.Random(seed * 7919 + handlerCount);

            var stranger = new TestHandler("stranger");
            var handlers = new TestHandler[handlerCount];
            var plans = new Plan[handlerCount];
            var actualLog = new List<string>();

            for (int i = 0; i < handlerCount; i++)
            {
                handlers[i] = new TestHandler("h" + i, i);
                handlers[i].OnTick = self =>
                {
                    actualLog.Add(self.Name);
                    Apply(driverOps, plans[self.Id], self, handlers, stranger);
                };
            }

            for (int pass = 0; pass < passes; pass++)
            {
                // Random calls between passes, applied identically to the manager and the model.
                int outsideOps = rng.Next(handlerCount / 2 + 1);
                for (int k = 0; k < outsideOps; k++)
                {
                    TestHandler target = handlers[rng.Next(handlerCount)];
                    switch (rng.Next(10))
                    {
                        case < 6:
                            driver.Register(target);
                            model.Register(target);
                            break;
                        case < 9:
                            driver.Deregister(target);
                            model.Deregister(target);
                            break;
                        default:
                            if (rng.Next(6) == 0)
                            {
                                driver.Clear();
                                model.Clear();
                            }

                            break;
                    }
                }

                for (int i = 0; i < handlerCount; i++)
                    plans[i] = RandomPlan(rng, handlerCount);

                // Model pass.
                var expected = new List<string>();
                model.Active.AddRange(model.Pending);
                model.Pending.Clear();
                TestHandler[] snapshot = model.Active.ToArray();
                for (int k = snapshot.Length - 1; k >= 0; k--)
                {
                    TestHandler handler = snapshot[k];
                    if (!model.Active.Contains(handler))
                        continue;

                    expected.Add(handler.Name);
                    Apply(model, plans[handler.Id], handler, handlers, stranger);
                }

                // Real pass.
                actualLog.Clear();
                driver.Tick();

                Assert.AreEqual(string.Join(",", expected), string.Join(",", actualLog),
                    $"Tick order diverged from the model (seed {seed}, {handlerCount} handlers, pass {pass}).");
            }
        }
    }
}