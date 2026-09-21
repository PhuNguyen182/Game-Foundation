#if USE_CSV_HELPER
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using CsvHelper.Configuration;
using DracoRuan.Foundation.DataFlow.StaticData.Controllers;
using DracoRuan.Foundation.DataFlow.StaticData.Decoders;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using NUnit.Framework;

namespace DracoRuan.Foundation.DataFlow.StaticData.Editor.Tests
{
    /// <summary>
    /// Covers CSV decoding and the index the record controller builds from it.
    /// </summary>
    public sealed class CsvRecordDecoderTests
    {
        private const string ValidCsv = "LevelId,DisplayName\n1,One\n2,Two\n";

        private CsvRecordDecoder<TestLevelRecord, TestLevelRecordMap> _decoder;

        [SetUp]
        public void SetUp() => this._decoder = new CsvRecordDecoder<TestLevelRecord, TestLevelRecordMap>();

        [Test]
        public void Decode_ParsesRows()
        {
            DecodeResult<IReadOnlyList<TestLevelRecord>> result = this.Decode(ValidCsv);

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.AreEqual(2, result.Value.Count);
            Assert.AreEqual("Two", result.Value[1].DisplayName);
        }

        [Test]
        public void Decode_TreatsAnEmptyTableAsSuccess()
        {
            // A header with no rows is a real, well-formed table. Whether a table is allowed to be
            // empty is the controller's call, not the parser's.
            DecodeResult<IReadOnlyList<TestLevelRecord>> result = this.Decode("LevelId,DisplayName\n");

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.AreEqual(0, result.Value.Count);
        }

        [Test]
        public void Decode_FailsOnAMalformedTable()
        {
            // The distinction the predecessor could not make: it returned an empty array for both a
            // parse failure and an empty table, so a broken CSV ended the fallback chain reporting
            // success with zero rows.
            DecodeResult<IReadOnlyList<TestLevelRecord>> result = this.Decode("WrongHeader\nnope\n");

            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.FailureReason);
        }

        [Test]
        public void Decode_RejectsAnAssetPayload()
        {
            DecodeResult<IReadOnlyList<TestLevelRecord>> result =
                this._decoder.Decode(StaticDataPayload.Missing());

            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void Controller_IndexesRecordsByKey()
        {
            using TestLevelController controller = BuildController(ValidCsv);
            controller.InitializeAsync().GetAwaiter().GetResult();

            Assert.IsTrue(controller.IsInitialized);
            Assert.AreEqual(2, controller.RecordsById.Count);
            Assert.IsTrue(controller.TryGet(2, out TestLevelRecord record));
            Assert.AreEqual("Two", record.DisplayName);
        }

        [Test]
        public void Controller_RejectsATableWithDuplicateKeys()
        {
            // Left unchecked, the table silently resolves to whichever duplicate was written last -
            // and which one that is depends on row order, so the bug moves when the CSV is re-sorted.
            using TestLevelController controller = BuildController("LevelId,DisplayName\n1,One\n1,Uno\n");

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("could not be loaded from any source"));
            controller.InitializeAsync().GetAwaiter().GetResult();

            Assert.IsFalse(controller.IsInitialized);
            Assert.AreEqual(StaticDataAttemptOutcome.ValidationFailed,
                controller.LastLoadResult.Attempts[0].Outcome);
            StringAssert.Contains("more than one row", controller.LastLoadResult.Attempts[0].Detail);
        }

        [Test]
        public void Controller_ExposesEmptyCollectionsBeforeLoading()
        {
            // Consumers resolved early must not have to null-check.
            using TestLevelController controller = BuildController(ValidCsv);

            Assert.IsNotNull(controller.Records);
            Assert.AreEqual(0, controller.Records.Count);
            Assert.IsFalse(controller.TryGet(1, out _));
        }

        private DecodeResult<IReadOnlyList<TestLevelRecord>> Decode(string csv) =>
            this._decoder.Decode(StaticDataPayload.FromText(csv));

        private static TestLevelController BuildController(string csv) =>
            new(new StaticDataControllerContext(new SingleTextSourceRegistry(csv)));

        public sealed class TestLevelRecord
        {
            public int LevelId { get; set; }
            public string DisplayName { get; set; }
        }

        public sealed class TestLevelRecordMap : ClassMap<TestLevelRecord>
        {
            public TestLevelRecordMap()
            {
                this.Map(record => record.LevelId).Name("LevelId");
                this.Map(record => record.DisplayName).Name("DisplayName");
            }
        }

        private sealed class TestLevelController
            : StaticRecordDataController<TestLevelRecord, TestLevelRecordMap, int>
        {
            public TestLevelController(StaticDataControllerContext context) : base(context)
            {
            }

            public override string DataId => "test_levels";

            protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =
                StaticDataSourceBinding.Chain(StaticDataSourceBinding.Resources("levels"));

            protected override int GetRecordKey(TestLevelRecord record) => record.LevelId;
        }

        private sealed class SingleTextSourceRegistry : IStaticDataSourceRegistry, IStaticDataSource
        {
            private readonly string _text;

            public SingleTextSourceRegistry(string text) => this._text = text;

            public StaticDataSourceType SourceType => StaticDataSourceType.Resources;

            public IStaticDataSource Get(StaticDataSourceType sourceType) =>
                sourceType == StaticDataSourceType.Resources ? this : null;

            public UniTask<StaticDataPayload> LoadAsync(string key, CancellationToken cancellationToken) =>
                UniTask.FromResult(StaticDataPayload.FromText(this._text));

            public void Release(in StaticDataPayload payload)
            {
            }
        }
    }
}
#endif
