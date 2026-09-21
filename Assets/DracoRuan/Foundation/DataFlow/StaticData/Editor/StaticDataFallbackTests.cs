using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.StaticData.Controllers;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DracoRuan.Foundation.DataFlow.StaticData.Editor.Tests
{
    /// <summary>
    /// Covers the fallback chain itself — which source wins, what happens when none does, and what is
    /// released afterwards.
    /// </summary>
    public sealed class StaticDataFallbackTests
    {
        private const string RemoteKey = "remote_key";
        private const string LocalKey = "local_path";

        [Test]
        public void Load_UsesTheFirstSourceThatHasAValue()
        {
            FakeSource remote = FakeSource.WithText(StaticDataSourceType.RemoteConfig, "{\"_maxRetries\": 5}");
            FakeSource local = FakeSource.WithText(StaticDataSourceType.Resources, "{\"_maxRetries\": 1}");

            using TestConfigController controller = Build(remote, local);
            Run(controller.InitializeAsync());

            Assert.IsTrue(controller.IsInitialized);
            Assert.AreEqual(5, controller.Data.MaxRetries);
            Assert.AreEqual(StaticDataSourceType.RemoteConfig, controller.LastLoadResult.WinningSource);
            Assert.AreEqual(0, local.LoadCount, "The second source should never have been asked.");
        }

        [Test]
        public void Load_FallsThroughWhenTheFirstSourceHasNothing()
        {
            FakeSource remote = FakeSource.Empty(StaticDataSourceType.RemoteConfig);
            FakeSource local = FakeSource.WithText(StaticDataSourceType.Resources, "{\"_maxRetries\": 1}");

            using TestConfigController controller = Build(remote, local);
            Run(controller.InitializeAsync());

            Assert.IsTrue(controller.IsInitialized);
            Assert.AreEqual(StaticDataSourceType.Resources, controller.LastLoadResult.WinningSource);
        }

        [Test]
        public void Load_FallsThroughWhenTheFirstSourceFailsValidation()
        {
            // The case the old chain got wrong: it stopped at the first source that returned anything
            // at all, so a stale or malformed remote payload beat the asset shipped in the build.
            FakeSource remote = FakeSource.WithText(StaticDataSourceType.RemoteConfig, "{\"_maxRetries\": -3}");
            FakeSource local = FakeSource.WithText(StaticDataSourceType.Resources, "{\"_maxRetries\": 4}");

            using TestConfigController controller = Build(remote, local);
            Run(controller.InitializeAsync());

            Assert.IsTrue(controller.IsInitialized);
            Assert.AreEqual(4, controller.Data.MaxRetries);
            Assert.AreEqual(StaticDataSourceType.Resources, controller.LastLoadResult.WinningSource);
            Assert.AreEqual(StaticDataAttemptOutcome.ValidationFailed,
                controller.LastLoadResult.Attempts[0].Outcome);
        }

        [Test]
        public void Load_ReportsNotInitializedWhenEverySourceFails()
        {
            // The old controllers set their initialized flag here anyway, with null data, so nothing
            // downstream could tell a loaded table from a broken one.
            FakeSource remote = FakeSource.Empty(StaticDataSourceType.RemoteConfig);
            FakeSource local = FakeSource.Empty(StaticDataSourceType.Resources);

            using TestConfigController controller = Build(remote, local);

            ExpectExhaustedChainError();
            Run(controller.InitializeAsync());

            Assert.IsFalse(controller.IsInitialized);
            Assert.IsFalse(controller.LastLoadResult.Succeeded);
            Assert.AreEqual(StaticDataSourceType.None, controller.LastLoadResult.WinningSource);
        }

        [Test]
        public void Load_SkipsASourceThisBuildDoesNotHave()
        {
            FakeSource local = FakeSource.WithText(StaticDataSourceType.Resources, "{\"_maxRetries\": 2}");

            // Remote deliberately absent, as in a project that registered no remote config reader.
            using TestConfigController controller = Build(local);
            Run(controller.InitializeAsync());

            Assert.IsTrue(controller.IsInitialized);
            Assert.AreEqual(StaticDataAttemptOutcome.SourceUnavailable,
                controller.LastLoadResult.Attempts[0].Outcome);
        }

        [Test]
        public void OnDataLoaded_FiresImmediatelyForASubscriberThatArrivesLate()
        {
            // Config loads during boot, so most consumers subscribe after the fact. With a plain
            // event they would wait for a second load that never comes.
            FakeSource local = FakeSource.WithText(StaticDataSourceType.Resources, "{\"_maxRetries\": 2}");

            using TestConfigController controller = Build(local);
            Run(controller.InitializeAsync());

            bool notified = false;
            controller.OnDataLoaded += () => notified = true;

            Assert.IsTrue(notified);
        }

        [Test]
        public void Reload_KeepsTheCurrentDataWhenTheNewLoadFails()
        {
            // A bad remote push should read as "the config did not change", not as the live session
            // losing its table.
            FakeSource local = FakeSource.WithText(StaticDataSourceType.Resources, "{\"_maxRetries\": 2}");

            using TestConfigController controller = Build(local);
            Run(controller.InitializeAsync());

            local.SetPayload(StaticDataPayload.Missing());

            ExpectExhaustedChainError();
            Run(controller.ReloadAsync());

            Assert.IsTrue(controller.IsInitialized);
            Assert.AreEqual(2, controller.Data.MaxRetries);
        }

        [Test]
        public void Load_ReleasesThePayloadOfEverySourceThatDidNotWin()
        {
            FakeSource remote = FakeSource.WithText(StaticDataSourceType.RemoteConfig, "{\"_maxRetries\": -1}");
            FakeSource local = FakeSource.WithText(StaticDataSourceType.Resources, "{\"_maxRetries\": 3}");

            using TestConfigController controller = Build(remote, local);
            Run(controller.InitializeAsync());

            Assert.AreEqual(1, remote.ReleaseCount, "The rejected payload must be released.");
            Assert.AreEqual(0, local.ReleaseCount, "The winning payload is released on dispose, not before.");
        }

        [Test]
        public void Dispose_ReleasesTheWinningPayload()
        {
            FakeSource local = FakeSource.WithText(StaticDataSourceType.Resources, "{\"_maxRetries\": 3}");

            TestConfigController controller = Build(local);
            Run(controller.InitializeAsync());
            controller.Dispose();

            Assert.AreEqual(1, local.ReleaseCount);
            Assert.IsFalse(controller.IsInitialized);
        }

        [Test]
        public void Load_PropagatesCancellationRatherThanTreatingItAsAMiss()
        {
            // Teardown must not look like "this source had nothing", or the chain would keep loading
            // while the app is shutting down.
            FakeSource local = FakeSource.WithText(StaticDataSourceType.Resources, "{\"_maxRetries\": 3}");

            using TestConfigController controller = Build(local);
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => Run(controller.InitializeAsync(cancellation.Token)));
            Assert.IsFalse(controller.IsInitialized);
        }

        /// <summary>
        /// Every fake source completes synchronously, so the load finishes before this returns.
        /// </summary>
        private static void Run(UniTask task) => task.GetAwaiter().GetResult();

        private static TestConfigController Build(params FakeSource[] sources)
        {
            FakeSourceRegistry registry = new(sources);
            return new TestConfigController(new StaticDataControllerContext(registry));
        }

        /// <summary>
        /// An exhausted chain is reported as an error, which the runner would otherwise fail the test
        /// for. Expecting this specific message keeps the assertion meaningful: flipping
        /// <c>LogAssert.ignoreFailingMessages</c> would also swallow unrelated errors, and it is a
        /// sticky global that leaks into every test running after it.
        /// </summary>
        private static void ExpectExhaustedChainError() =>
            LogAssert.Expect(LogType.Error, new Regex("could not be loaded from any source"));

        private sealed class TestConfigController : StaticDataController<SampleConfigData>
        {
            public TestConfigController(StaticDataControllerContext context) : base(context)
            {
            }

            public override string DataId => "test_config";

            protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =
                StaticDataSourceBinding.Chain(
                    StaticDataSourceBinding.RemoteConfig(RemoteKey),
                    StaticDataSourceBinding.Resources(LocalKey));

            protected override bool Validate(SampleConfigData data, out string failureReason)
            {
                if (data.MaxRetries <= 0)
                {
                    failureReason = "MaxRetries must be positive.";
                    return false;
                }

                failureReason = null;
                return true;
            }
        }

        private sealed class FakeSourceRegistry : IStaticDataSourceRegistry
        {
            private readonly Dictionary<StaticDataSourceType, IStaticDataSource> _sources = new();

            public FakeSourceRegistry(IEnumerable<FakeSource> sources)
            {
                foreach (FakeSource source in sources)
                    this._sources[source.SourceType] = source;
            }

            public IStaticDataSource Get(StaticDataSourceType sourceType) =>
                this._sources.GetValueOrDefault(sourceType);
        }

        private sealed class FakeSource : IStaticDataSource
        {
            private StaticDataPayload _payload;

            private FakeSource(StaticDataSourceType sourceType, StaticDataPayload payload)
            {
                this.SourceType = sourceType;
                this._payload = payload;
            }

            public StaticDataSourceType SourceType { get; }
            public int LoadCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public static FakeSource WithText(StaticDataSourceType sourceType, string text) =>
                new(sourceType, StaticDataPayload.FromText(text));

            public static FakeSource Empty(StaticDataSourceType sourceType) =>
                new(sourceType, StaticDataPayload.Missing());

            public void SetPayload(StaticDataPayload payload) => this._payload = payload;

            public UniTask<StaticDataPayload> LoadAsync(string key, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.LoadCount++;
                return UniTask.FromResult(this._payload);
            }

            public void Release(in StaticDataPayload payload)
            {
                if (payload.HasValue)
                    this.ReleaseCount++;
            }
        }
    }
}