using System;
using DracoRuan.Foundation.DataFlow.StaticData.Decoders;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using Newtonsoft.Json;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.StaticData.Controllers
{
    /// <summary>
    /// Base class for a plain config table: one ScriptableObject, loaded from the first source that
    /// can supply a valid copy.
    ///
    /// <code>
    /// [StaticDataId(GachaRateController.Id)]
    /// public sealed class GachaRateController : StaticDataController&lt;GachaRateData&gt;
    /// {
    ///     public const string Id = "gacha_rate";
    ///
    ///     public GachaRateController(StaticDataControllerContext context) : base(context) { }
    ///
    ///     public override string DataId =&gt; Id;
    ///
    ///     protected override IReadOnlyList&lt;StaticDataSourceBinding&gt; Sources { get; } =
    ///         StaticDataSourceBinding.Chain(
    ///             StaticDataSourceBinding.RemoteConfig("gacha_rate"),
    ///             StaticDataSourceBinding.Addressable("Configs/GachaRateData"));
    ///
    ///     protected override bool Validate(GachaRateData data, out string failureReason) =&gt;
    ///         data.IsWellFormed(out failureReason);
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// <para><b><typeparamref name="TData"/> is a ScriptableObject, on every source.</b> An asset
    /// source hands one over directly; a text source builds one with
    /// <c>ScriptableObject.CreateInstance</c> and populates it from JSON. The predecessor used a JSON
    /// deserializer for every source, which cannot produce a ScriptableObject at all — so a chain
    /// spanning remote config and a shipped asset could never have worked.</para>
    ///
    /// <para><b>The file is named after what the class is, not what it is called.</b> Its previous
    /// name, <c>StaticDataController.cs</c>, shared a prefix with three siblings —
    /// <c>StaticDataControllerBase</c>, <c>StaticDataControllerContext</c> and a
    /// <c>StaticDataControllerAttribute</c> that was later renamed — and a rename made outside the
    /// Editor left Unity's move tracking pointing this path at the wrong asset, so the file was
    /// dropped from script compilation entirely while its siblings compiled normally.</para>
    /// </remarks>
    public abstract class StaticDataController<TData> : StaticDataControllerBase<TData>
        where TData : ScriptableObject
    {
        private IStaticDataDecoder<TData> _textDecoder;
        private IStaticDataDecoder<TData> _assetDecoder;

        protected StaticDataController(StaticDataControllerContext context) : base(context)
        {
        }

        public override Type DataType => typeof(TData);

        protected override IStaticDataDecoder<TData> GetDecoder(StaticDataPayloadKind payloadKind)
        {
            switch (payloadKind)
            {
                case StaticDataPayloadKind.Asset:
                    return this._assetDecoder ??= new AssetPassthroughDecoder<TData>();

                // Text covers remote config, a downloaded blob, and a .json TextAsset sitting in
                // Resources or an Addressables group - one decoder, because by this point the three
                // are indistinguishable and should be.
                case StaticDataPayloadKind.Text:
                    return this._textDecoder ??= new JsonOverwriteDecoder<TData>(this.CreateJsonSettings());

                default:
                    return null;
            }
        }

        /// <summary>
        /// Override to supply Newtonsoft settings of your own — an extra converter, for instance.
        /// Returning null uses <see cref="StaticDataJson.Settings"/>. Any replacement must keep
        /// <see cref="UnitySerializationContractResolver"/>, or private <c>[SerializeField]</c>
        /// fields silently stop being populated and remote config appears to return an empty object.
        /// </summary>
        protected virtual JsonSerializerSettings CreateJsonSettings() => null;
    }
}
