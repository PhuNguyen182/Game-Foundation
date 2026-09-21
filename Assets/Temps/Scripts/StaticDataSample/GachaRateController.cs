using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.StaticData;
using DracoRuan.Foundation.DataFlow.StaticData.Controllers;
using DracoRuan.Foundation.DataFlow.StaticData.Decoders;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;

namespace Temps.Scripts.StaticDataSample
{
    /// <summary>
    /// Sample of the plain-config branch: remote config first, the shipped asset as the floor.
    /// </summary>
    /// <remarks>
    /// Both links produce the same <see cref="GachaRateData"/> — one by populating a fresh instance
    /// from JSON, one by handing over the asset. That is the case the previous implementation could
    /// not express at all, because it ran every source through a JSON deserializer.
    /// </remarks>
    [StaticDataId(Id)]
    public sealed class GachaRateController : StaticDataControllerBase<GachaRateData>
    {
        public const string Id = "gacha_rate";

        public GachaRateController(StaticDataControllerContext context) : base(context)
        {
        }

        private IStaticDataDecoder<GachaRateData> _textDecoder;
        private IStaticDataDecoder<GachaRateData> _assetDecoder;

        public override string DataId => Id;

        // Expression-bodied, not an auto-property: `{ get; }` with no initializer compiles fine and
        // returns null forever, which would surface much later as a null in tooling rather than here.
        public override Type DataType => typeof(GachaRateData);

        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =
            StaticDataSourceBinding.Chain(
                StaticDataSourceBinding.RemoteConfig("gacha_rate"),
                StaticDataSourceBinding.Resources("Configs/GachaRateData"));

        /// <summary>
        /// Picks the decoder from the shape of what arrived, not from which source it came from: the
        /// local copy ships as <c>Resources/Configs/GachaRateData.json</c> (a TextAsset, so text), and
        /// swapping it for an authored <c>GachaRateData.asset</c> at the same path needs no change
        /// here.
        /// </summary>
        /// <remarks>
        /// Both decoders are cached because <see cref="StaticDataControllerBase{T}.ReloadAsync"/> runs
        /// the chain again on every remote push, and a new decoder per attempt would rebuild
        /// Newtonsoft's contract cache each time.
        /// </remarks>
        protected override IStaticDataDecoder<GachaRateData> GetDecoder(StaticDataPayloadKind payloadKind)
        {
            switch (payloadKind)
            {
                case StaticDataPayloadKind.Asset:
                    return this._assetDecoder ??= new AssetPassthroughDecoder<GachaRateData>();

                case StaticDataPayloadKind.Text:
                    return this._textDecoder ??= new JsonOverwriteDecoder<GachaRateData>();

                // Returning null rejects the payload and sends the chain to the next source.
                default:
                    return null;
            }
        }

        protected override bool Validate(GachaRateData data, out string failureReason) =>
            data.IsWellFormed(out failureReason);
    }
}