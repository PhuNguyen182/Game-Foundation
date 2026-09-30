using System;
using System.Diagnostics;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices.Tests
{
    /// <summary>
    /// Performance and memory benchmarks of the update / fixed-update service managers at mid-core scale
    /// (thousands of live handlers, hundreds of spawns and despawns per frame).
    /// <para>
    /// Allocations are counted exactly with a <see cref="ProfilerRecorder"/> on "GC Allocated In Frame" (see the
    /// CompleteTimer benchmark for why <c>GC.GetAllocatedBytesForCurrentThread</c> is not used); a control allocation of
    /// known size proves the recorder works. Retained memory is the Mono heap after a full collection. Timings vary by
    /// machine, so they are reported and only checked against generous bounds that separate linear from quadratic
    /// behaviour; allocation goals are asserted tightly.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class UpdateServiceBenchmarkTests
    {
        /// <summary>Below this many bytes per operation counts as "zero allocation" once the recorder's overhead is subtracted.</summary>
        private const double NoiseBytesPerOp = 1.0;

        /// <summary>
        /// Tick windows are short, so the Editor's ~0.5 KB noise chunks weigh more per tick. A real per-tick allocation
        /// is at least one 24-byte object, six times this budget.
        /// </summary>
        private const double TickNoiseBytesPerTick = 4.0;

        /// <summary>Upper bound for work that must be linear in the handler count; a quadratic implementation exceeds it by an order of magnitude.</summary>
        private const double LinearWorkBudgetMs = 250;

        private static string _tag = "";
        private static long? _windowOverheadBytes;

        private sealed class NoopHandler : IUpdateHandler, IFixedUpdateHandler
        {
            public int Ticks;

            void IUpdateHandler.Tick(float deltaTime) => this.Ticks++;
            void IFixedUpdateHandler.Tick() => this.Ticks++;
        }

        private sealed class SelfRemovingHandler : IUpdateHandler
        {
            public int Ticks;

            public void Tick(float deltaTime)
            {
                this.Ticks++;
                UpdateServiceManager.DeregisterUpdateHandler(this);
            }
        }

        private readonly struct Result
        {
            public readonly double Ms;
            public readonly long AllocatedBytes;
            public readonly long HeapDeltaBytes;
            public readonly int Collections;
            public readonly int Operations;

            public double BytesPerOp => this.AllocatedBytes / (double)Math.Max(1, this.Operations);

            public Result(double ms, long allocatedBytes, long heapDeltaBytes, int collections, int operations)
            {
                this.Ms = ms;
                this.AllocatedBytes = allocatedBytes;
                this.HeapDeltaBytes = heapDeltaBytes;
                this.Collections = collections;
                this.Operations = operations;
            }
        }

        [SetUp]
        public void SetUp()
        {
            UpdateServiceManager.Clear();
            FixedUpdateServiceManager.Clear();
            UpdateServiceManager.UpdateTime();
            FixedUpdateServiceManager.FixedUpdateTime();
        }

        [TearDown]
        public void TearDown()
        {
            UpdateServiceManager.Clear();
            FixedUpdateServiceManager.Clear();
        }

        [Test]
        public void MemoryMeasurer_DetectsKnownAllocation()
        {
            _tag = "control";
            const int arrays = 1000;
            const int arraySize = 1000;
            object[] sink = new object[arrays];

            Result r = Measure("Control: 1,000 x new byte[1000]", arrays, () =>
            {
                for (int i = 0; i < arrays; i++)
                    sink[i] = new byte[arraySize];
            });

            GC.KeepAlive(sink);
            Assert.That(r.AllocatedBytes, Is.GreaterThanOrEqualTo(arrays * arraySize),
                "Measurer missed a known 1,000,000-byte allocation - memory numbers can't be trusted.");
        }

        [TestCase(1_000)]
        [TestCase(10_000)]
        public void Tick_ManyHandlers_IsAllocationFree_AndReportsCostPerHandler(int count)
        {
            _tag = $"tick n={count:N0}";
            const int ticks = 1000;

            NoopHandler[] handlers = NewHandlers(count);

            Result register = Measure("Register xN (includes growth of the internal collections)", count, () =>
            {
                for (int i = 0; i < count; i++)
                    UpdateServiceManager.RegisterUpdateHandler(handlers[i]);
            });

            UpdateServiceManager.UpdateTime(); // flush
            UpdateServiceManager.UpdateTime();

            Result tick = MeasureBest($"UpdateTime x{ticks} over N handlers", ticks, () =>
            {
                for (int i = 0; i < ticks; i++)
                    UpdateServiceManager.UpdateTime();
            });
            Report(
                $"  = {tick.Ms * 1e6 / ((double)ticks * count):F1} ns per handler-tick, {tick.Ms / ticks * 1000:F1} us per frame");

            GC.KeepAlive(handlers);
            Assert.That(tick.BytesPerOp, Is.LessThan(TickNoiseBytesPerTick),
                $"A steady-state tick must not allocate ({tick.AllocatedBytes:N0} B over {ticks} ticks).");
            Assert.That(tick.Ms / ticks / count * 1e6, Is.LessThan(1000),
                "More than a microsecond per handler-tick: something is badly wrong.");
            Assert.That(register.Ms, Is.LessThan(LinearWorkBudgetMs), "Registering N handlers must be linear.");
        }

        [Test]
        public void FixedTick_ManyHandlers_IsAllocationFree()
        {
            const int count = 5_000;
            const int ticks = 1000;
            _tag = $"fixed n={count:N0}";

            NoopHandler[] handlers = NewHandlers(count);
            for (int i = 0; i < count; i++)
                FixedUpdateServiceManager.RegisterFixedUpdateHandler(handlers[i]);
            FixedUpdateServiceManager.FixedUpdateTime();
            FixedUpdateServiceManager.FixedUpdateTime();

            Result tick = MeasureBest($"FixedUpdateTime x{ticks} over N handlers", ticks, () =>
            {
                for (int i = 0; i < ticks; i++)
                    FixedUpdateServiceManager.FixedUpdateTime();
            });
            Report($"  = {tick.Ms * 1e6 / ((double)ticks * count):F1} ns per handler-tick");

            GC.KeepAlive(handlers);
            Assert.That(tick.BytesPerOp, Is.LessThan(TickNoiseBytesPerTick));
        }

        [Test]
        public void SpawnDespawnChurn_SteadyState_DoesNotAllocate()
        {
            // Object-pool pattern: each frame a batch of live handlers is despawned and the same number respawned.
            const int live = 2_000;
            const int batch = 100;
            const int warmupFrames = 50;
            const int frames = 300;
            _tag = $"churn live={live:N0} batch={batch}";

            NoopHandler[] handlers = NewHandlers(live);
            for (int i = 0; i < live; i++)
                UpdateServiceManager.RegisterUpdateHandler(handlers[i]);
            UpdateServiceManager.UpdateTime();
            UpdateServiceManager.UpdateTime();

            var rng = new System.Random(12345);
            int[] order = new int[live];
            for (int i = 0; i < live; i++)
                order[i] = i;

            void Frame()
            {
                // Despawn `batch` handlers chosen at random, respawn a different `batch`.
                for (int k = 0; k < batch; k++)
                {
                    int a = rng.Next(live), b = rng.Next(live);
                    UpdateServiceManager.DeregisterUpdateHandler(handlers[a]);
                    UpdateServiceManager.RegisterUpdateHandler(handlers[b]);
                }

                UpdateServiceManager.UpdateTime();
            }

            for (int f = 0; f < warmupFrames; f++)
                Frame();

            Result churn = Measure($"{frames} frames of {batch} despawns + {batch} spawns over {live:N0} live handlers",
                frames * batch * 2, () =>
                {
                    for (int f = 0; f < frames; f++)
                        Frame();
                });
            Report($"  = {churn.Ms / frames * 1000:F1} us per frame");

            GC.KeepAlive(handlers);
            Assert.That(churn.BytesPerOp, Is.LessThan(NoiseBytesPerOp),
                $"Register/Deregister/tick churn must not allocate in steady state ({churn.AllocatedBytes:N0} B).");
            Assert.That(churn.Ms, Is.LessThan(LinearWorkBudgetMs * 4));
        }

        [TestCase(5_000)]
        [TestCase(20_000)]
        public void MassDeregister_InRandomOrder_IsLinear(int count)
        {
            _tag = $"mass-deregister n={count:N0}";

            NoopHandler[] handlers = NewHandlers(count);
            for (int i = 0; i < count; i++)
                UpdateServiceManager.RegisterUpdateHandler(handlers[i]);
            UpdateServiceManager.UpdateTime();
            UpdateServiceManager.UpdateTime();

            Shuffle(handlers, new System.Random(count));

            Result dereg = Measure("Deregister xN in random order", count, () =>
            {
                for (int i = 0; i < count; i++)
                    UpdateServiceManager.DeregisterUpdateHandler(handlers[i]);
            });
            Result after = Measure("UpdateTime after everything was removed", 1, UpdateServiceManager.UpdateTime);

            foreach (NoopHandler handler in handlers)
                handler.Ticks = 0;
            UpdateServiceManager.UpdateTime();
            UpdateServiceManager.UpdateTime();
            foreach (NoopHandler handler in handlers)
                Assert.AreEqual(0, handler.Ticks, "A deregistered handler kept ticking.");

            Assert.That(dereg.Ms + after.Ms, Is.LessThan(LinearWorkBudgetMs),
                $"Removing {count:N0} handlers took {dereg.Ms + after.Ms:F1} ms - removal is not linear.");
            Assert.That(dereg.BytesPerOp, Is.LessThan(NoiseBytesPerOp));
        }

        [Test]
        public void MassSelfCompletion_InOneFrame_TicksEveryHandlerOnce_AndIsLinear()
        {
            // E.g. 20,000 timers or despawn-by-timer objects all expiring in the same frame.
            const int count = 20_000;
            _tag = $"mass-self-completion n={count:N0}";

            var handlers = new SelfRemovingHandler[count];
            for (int i = 0; i < count; i++)
                handlers[i] = new SelfRemovingHandler();
            for (int i = 0; i < count; i++)
                UpdateServiceManager.RegisterUpdateHandler(handlers[i]);

            Result completion = Measure("Two passes in which every handler deregisters itself", count, () =>
            {
                UpdateServiceManager.UpdateTime();
                UpdateServiceManager.UpdateTime();
            });

            int missed = 0, doubled = 0;
            foreach (SelfRemovingHandler handler in handlers)
            {
                if (handler.Ticks == 0) missed++;
                else if (handler.Ticks > 1) doubled++;
            }

            UpdateServiceManager.UpdateTime();
            GC.KeepAlive(handlers);

            Assert.AreEqual(0, missed, "Handlers were skipped while others removed themselves.");
            Assert.AreEqual(0, doubled, "Handlers ticked again after deregistering themselves.");
            Assert.That(completion.Ms, Is.LessThan(LinearWorkBudgetMs),
                $"Mass self-completion took {completion.Ms:F1} ms - removal is not linear.");
        }

        [Test]
        public void SpawnAndDespawnInTheSameFrame_DoesNotLeakOrTick()
        {
            // A pooled object enabled and disabled within one frame, repeatedly, must leave nothing behind.
            const int pooled = 1_000;
            const int frames = 100;
            _tag = "same-frame spawn/despawn";

            // A level load registers thousands of handlers at once and leaves the registry's internal collections
            // large; the cost of later register/deregister calls must not depend on that size.
            NoopHandler[] levelLoad = NewHandlers(30_000);
            foreach (NoopHandler handler in levelLoad)
                UpdateServiceManager.RegisterUpdateHandler(handler);
            UpdateServiceManager.UpdateTime();
            UpdateServiceManager.Clear();

            NoopHandler[] handlers = NewHandlers(pooled);
            UpdateServiceManager.UpdateTime();
            long retainedBefore = RetainedBytes();

            Result churn = Measure($"{frames} frames x {pooled:N0} register+deregister pairs", frames * pooled * 2,
                () =>
                {
                    for (int f = 0; f < frames; f++)
                    {
                        for (int i = 0; i < pooled; i++)
                        {
                            UpdateServiceManager.RegisterUpdateHandler(handlers[i]);
                            UpdateServiceManager.DeregisterUpdateHandler(handlers[i]);
                        }

                        UpdateServiceManager.UpdateTime();
                    }
                });

            long leaked = RetainedBytes() - retainedBefore;
            Report($"Retained after the churn (post-GC): {leaked:N0} B");

            UpdateServiceManager.UpdateTime();
            GC.KeepAlive(handlers);

            int ticked = 0;
            foreach (NoopHandler handler in handlers)
                ticked += handler.Ticks;

            Assert.AreEqual(0, ticked,
                "Handlers that were deregistered in the frame they were registered still ticked.");
            Assert.That(leaked, Is.LessThan(64 * 1024),
                "Register/Deregister in the same frame leaks registry entries.");
            Assert.That(churn.BytesPerOp, Is.LessThan(NoiseBytesPerOp));
            Assert.That(churn.Ms, Is.LessThan(100),
                $"{frames * pooled * 2:N0} register/deregister calls took {churn.Ms:F1} ms " +
                $"({churn.Ms * 1e6 / (frames * pooled * 2):F0} ns per call).");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Tick_CostStaysWithinASmallMultipleOfABareArrayLoop(bool fixedUpdate)
        {
            // Machine-independent: the same handlers are ticked by the manager and by a minimal array loop in
            // interleaved runs, so scheduler noise hits both and only the ratio is asserted.
            const int count = 5_000;
            const int ticks = 500;
            const int runs = 7;
            _tag = fixedUpdate ? "tick-ratio fixed" : "tick-ratio update";

            NoopHandler[] handlers = NewHandlers(count);
            var bareFixed = new IFixedUpdateHandler[count];
            var bareUpdate = new IUpdateHandler[count];
            for (int i = 0; i < count; i++)
            {
                bareFixed[i] = handlers[i];
                bareUpdate[i] = handlers[i];
                if (fixedUpdate)
                    FixedUpdateServiceManager.RegisterFixedUpdateHandler(handlers[i]);
                else
                    UpdateServiceManager.RegisterUpdateHandler(handlers[i]);
            }

            if (fixedUpdate)
                FixedUpdateServiceManager.FixedUpdateTime();
            else
                UpdateServiceManager.UpdateTime();

            double bestManaged = double.MaxValue, bestBare = double.MaxValue;
            for (int run = 0; run < runs; run++)
            {
                var watch = Stopwatch.StartNew();
                for (int t = 0; t < ticks; t++)
                {
                    if (fixedUpdate)
                        FixedUpdateServiceManager.FixedUpdateTime();
                    else
                        UpdateServiceManager.UpdateTime();
                }

                bestManaged = Math.Min(bestManaged, watch.Elapsed.TotalMilliseconds);

                watch.Restart();
                for (int t = 0; t < ticks; t++)
                {
                    if (fixedUpdate)
                    {
                        for (int i = count - 1; i >= 0; i--)
                            bareFixed[i].Tick();
                    }
                    else
                    {
                        for (int i = count - 1; i >= 0; i--)
                            bareUpdate[i].Tick(0.016f);
                    }
                }

                bestBare = Math.Min(bestBare, watch.Elapsed.TotalMilliseconds);
            }

            double perHandlerManaged = bestManaged * 1e6 / ((double)ticks * count);
            double perHandlerBare = bestBare * 1e6 / ((double)ticks * count);
            Report($"manager {perHandlerManaged:F1} ns vs bare array loop {perHandlerBare:F1} ns per handler-tick " +
                   $"(x{bestManaged / bestBare:F2})");

            GC.KeepAlive(handlers);
            Assert.That(bestManaged / bestBare, Is.LessThan(2.5),
                "The tick loop costs far more than iterating a plain array.");
        }

        [Test]
        public void Registry_RetainedMemoryPerLiveHandler_IsSmall([Values(1_000, 10_000)] int count)
        {
            // Measured on an isolated registry: the static managers keep whatever capacity earlier tests grew them to.
            _tag = $"registry-memory n={count:N0}";

            long emptyBefore = RetainedBytes();
            var empty = new HandlerRegistry<IUpdateHandler>(UpdateHandlerConstants.InitializedCapacity);
            long emptyFootprint = RetainedBytes() - emptyBefore;
            GC.KeepAlive(empty);
            Report(
                $"Idle footprint of a registry at the default capacity ({UpdateHandlerConstants.InitializedCapacity:N0}): " +
                $"{emptyFootprint:N0} B");

            NoopHandler[] handlers = NewHandlers(count);
            long before = RetainedBytes();
            var registry = new HandlerRegistry<IUpdateHandler>(UpdateHandlerConstants.InitializedCapacity);
            for (int i = 0; i < count; i++)
                registry.Register(handlers[i]);
            registry.BeginTick();
            registry.EndTick();

            long retained = RetainedBytes() - before;
            double perHandler = retained / (double)count;
            Report(
                $"Retained by a registry holding N live handlers (post-GC): {retained:N0} B = {perHandler:F1} B/handler");

            GC.KeepAlive(registry);
            GC.KeepAlive(handlers);
            Assert.AreEqual(count, registry.ActiveCount);

            // The Mono heap reading moves in 4 KB blocks and depends on when the conservative GC frees the arrays
            // left behind by growth, so it only guards against going back to a ~107 B/handler Dictionary design.
            Assert.That(perHandler, Is.LessThan(64), "Registry memory per live handler is too large.");

            // The exact figure: the registry's backing arrays.
            double arrayBytesPerHandler = registry.ArrayBytes / (double)count;
            Report($"Backing arrays: {registry.ArrayBytes:N0} B = {arrayBytesPerHandler:F1} B/handler; " +
                   $"empty registry at the default capacity: {empty.ArrayBytes:N0} B");
            Assert.That(arrayBytesPerHandler, Is.LessThan(32), "Registry arrays per live handler are too large.");
            Assert.That(empty.ArrayBytes, Is.LessThan(32 * 1024), "An empty registry at the default capacity is too large.");
        }

        private static NoopHandler[] NewHandlers(int count)
        {
            var handlers = new NoopHandler[count];
            for (int i = 0; i < count; i++)
                handlers[i] = new NoopHandler();
            return handlers;
        }

        private static void Shuffle<T>(T[] array, System.Random rng)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        /// <summary>Live bytes on the Mono heap after a full, finalizer-complete collection.</summary>
        private static long RetainedBytes()
        {
            CollectFully();
            return Profiler.GetMonoUsedSizeLong();
        }

        private static void CollectFully()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static Result Measure(string label, int operations, Action action)
        {
            _windowOverheadBytes ??= CalibrateWindowOverhead();

            Result raw = MeasureWindow(action, operations);
            var result = new Result(raw.Ms, Math.Max(0, raw.AllocatedBytes - _windowOverheadBytes.Value),
                raw.HeapDeltaBytes, raw.Collections, operations);

            Report(
                $"{label}: {result.Ms:F3} ms, allocated {result.AllocatedBytes:N0} B " +
                $"({result.BytesPerOp:F2} B/op), " +
                $"mono heap {result.HeapDeltaBytes:+#,0;-#,0;0} B, gen0 GCs {result.Collections}");
            return result;
        }

        /// <summary>Smallest allocation reported over several empty windows: the measurement floor.</summary>
        private static long CalibrateWindowOverhead()
        {
            long floor = long.MaxValue;
            for (int i = 0; i < 10; i++)
                floor = Math.Min(floor, MeasureWindow(static () => { }, 1).AllocatedBytes);

            Report($"Measurement floor (empty window): {floor:N0} B");
            return floor;
        }

        private static Result MeasureWindow(Action action, int operations)
        {
            CollectFully();

            using ProfilerRecorder allocRecorder =
                ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            int collectionsBefore = GC.CollectionCount(0);
            long heapBefore = Profiler.GetMonoUsedSizeLong();
            long allocBefore = allocRecorder.CurrentValue;
            var stopwatch = Stopwatch.StartNew();

            action();

            stopwatch.Stop();
            long allocAfter = allocRecorder.CurrentValue;
            long heapAfter = Profiler.GetMonoUsedSizeLong();
            int collections = GC.CollectionCount(0) - collectionsBefore;

            return new Result(stopwatch.Elapsed.TotalMilliseconds, allocAfter - allocBefore, heapAfter - heapBefore,
                collections, operations);
        }

        private static void Report(string message)
        {
            string line = $"[Bench][{_tag}] {message}";
            NUnit.Framework.TestContext.WriteLine(line);
            NUnit.Framework.TestContext.Progress.WriteLine(line);
            UnityEngine.Debug.Log(line);
        }

        /// <summary>
        /// Repeats a window and keeps the run with the fewest allocated bytes. The Editor allocates a few hundred bytes
        /// of background noise at random; real per-call allocation shows up in every run, noise in some.
        /// </summary>
        private static Result MeasureBest(string label, int operations, Action action, int runs = 5)
        {
            Result best = default;
            for (int run = 0; run < runs; run++)
            {
                Result r = Measure($"{label} (run {run + 1})", operations, action);
                if (run == 0 || r.AllocatedBytes < best.AllocatedBytes)
                    best = r;
            }

            return best;
        }
    }
}