using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.StaticData.Decoders;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using NUnit.Framework;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.StaticData.Editor.Tests
{
    /// <summary>
    /// Covers the decode path that turns a remote JSON payload into a config ScriptableObject.
    /// </summary>
    /// <remarks>
    /// These tests exist because every failure mode here is silent. A resolver that misses
    /// <c>[SerializeField]</c> members, a populate call that resets absent fields, an unknown member
    /// that is quietly ignored — none of them throw, and all of them produce a config object the game
    /// will happily run on.
    /// </remarks>
    public sealed class JsonOverwriteDecoderTests
    {
        private JsonOverwriteDecoder<SampleConfigData> _decoder;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp() => this._decoder = new JsonOverwriteDecoder<SampleConfigData>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in this._created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            this._created.Clear();
        }

        [Test]
        public void Decode_PopulatesPrivateSerializeFieldMembers()
        {
            // The single most important assertion in this file. Newtonsoft's defaults see only public
            // members, so without UnitySerializationContractResolver this returns an object full of
            // zeroes - with no exception - and, because remote config is the first link in a chain,
            // that empty object beats the asset shipped in the build.
            DecodeResult<SampleConfigData> result = this.Decode("{\"_maxRetries\": 7, \"_label\": \"live\"}");

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.AreEqual(7, result.Value.MaxRetries);
            Assert.AreEqual("live", result.Value.Label);
        }

        [Test]
        public void Decode_LeavesAbsentFieldsAtTheirAuthoredDefaults()
        {
            // PopulateObject semantics, and the reason DeserializeObject is not used: a payload that
            // only overrides one field must not zero out everything it did not mention.
            DecodeResult<SampleConfigData> result = this.Decode("{\"_maxRetries\": 2}");

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.AreEqual(2, result.Value.MaxRetries);
            Assert.AreEqual(SampleConfigData.DefaultLabel, result.Value.Label);
        }

        [Test]
        public void Decode_FailsOnAnUnknownMember()
        {
            // A payload whose shape has drifted from this build. Failing sends the chain to the
            // shipped asset; ignoring the field would load a half-applied config and call it success.
            DecodeResult<SampleConfigData> result = this.Decode("{\"_maxRetries\": 3, \"_removedField\": 1}");

            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.FailureReason);
        }

        [Test]
        public void Decode_FailsOnMalformedJsonWithoutLeakingTheInstance()
        {
            DecodeResult<SampleConfigData> result = this.Decode("{ this is not json");

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Value);
        }

        [Test]
        public void Decode_SupportsDictionaries()
        {
            // The capability Newtonsoft was chosen over JsonUtility for. If this breaks, the engine
            // choice no longer buys anything.
            DecodeResult<SampleConfigData> result =
                this.Decode("{\"_thresholdsByTier\": {\"bronze\": 10, \"gold\": 90}}");

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.AreEqual(2, result.Value.ThresholdsByTier.Count);
            Assert.AreEqual(90, result.Value.ThresholdsByTier["gold"]);
        }

        [Test]
        public void Decode_ReplacesCollectionsRatherThanAppendingToThem()
        {
            // ObjectCreationHandling.Replace. The default would add the payload's entries to the ones
            // the ScriptableObject was created with, so a remote list of two would arrive as four.
            DecodeResult<SampleConfigData> result = this.Decode("{\"_tiers\": [\"a\", \"b\"]}");

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.AreEqual(2, result.Value.Tiers.Count);
        }

        [Test]
        public void Decode_RejectsAnAssetPayload()
        {
            SampleConfigData asset = ScriptableObject.CreateInstance<SampleConfigData>();
            this._created.Add(asset);

            DecodeResult<SampleConfigData> result = this._decoder.Decode(StaticDataPayload.FromAsset(asset));

            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void Decode_ReportsThatItOwnsWhatItCreated()
        {
            // Drives cleanup: an instance built here must be destroyed, while an asset handed over by
            // a source must not be.
            DecodeResult<SampleConfigData> result = this.Decode("{\"_maxRetries\": 1}");

            Assert.IsTrue(result.OwnsValue);
        }

        private DecodeResult<SampleConfigData> Decode(string json)
        {
            DecodeResult<SampleConfigData> result = this._decoder.Decode(StaticDataPayload.FromText(json));
            if (result.Value != null)
                this._created.Add(result.Value);

            return result;
        }
    }

    /// <summary>
    /// Written the way a real config asset is written: private fields with <c>[SerializeField]</c>,
    /// read through properties.
    /// </summary>
    public sealed class SampleConfigData : ScriptableObject
    {
        public const string DefaultLabel = "authored";

        [SerializeField] private int _maxRetries = 1;
        [SerializeField] private string _label = DefaultLabel;
        [SerializeField] private List<string> _tiers = new() { "seed" };

        // Not a Unity-serializable type; reachable only because the JSON engine is Newtonsoft.
        [SerializeField] private Dictionary<string, int> _thresholdsByTier = new();

        public int MaxRetries => this._maxRetries;
        public string Label => this._label;
        public IReadOnlyList<string> Tiers => this._tiers;
        public IReadOnlyDictionary<string, int> ThresholdsByTier => this._thresholdsByTier;
    }
}
