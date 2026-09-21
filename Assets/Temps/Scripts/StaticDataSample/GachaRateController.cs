using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.StaticData;
using DracoRuan.Foundation.DataFlow.StaticData.Controllers;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;

namespace Temps.Scripts.StaticDataSample
{
    /// <summary>
    /// Sample of the plain-config branch: remote config first, the shipped asset as the floor.
    /// </summary>
    /// <remarks>
    /// <para>Both links produce the same <see cref="GachaRateData"/> — one by populating a fresh
    /// instance from JSON, one by handing over the asset. That is the case the previous
    /// implementation could not express at all, because it ran every source through a JSON
    /// deserializer.</para>
    ///
    /// <para>Deriving from <see cref="StaticDataController{TData}"/> rather than
    /// <c>StaticDataControllerBase</c> is what makes this so short: the base already picks the
    /// decoder from the payload's shape and caches both, so a plain config table only has to say
    /// what it is called, where to look, and what counts as valid.</para>
    /// </remarks>
    [StaticDataId(Id)]
    public sealed class GachaRateController : StaticDataController<GachaRateData>
    {
        public const string Id = "gacha_rate";

        public GachaRateController(StaticDataControllerContext context) : base(context)
        {
        }

        public override string DataId => Id;

        /// <summary>
        /// Remote config first so rates can be retuned without a build, the shipped copy underneath
        /// so the table can never be missing.
        /// </summary>
        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =
            StaticDataSourceBinding.Chain(
                StaticDataSourceBinding.RemoteConfig("gacha_rate"),
                StaticDataSourceBinding.Resources("Configs/GachaRateData"));

        protected override bool Validate(GachaRateData data, out string failureReason) =>
            data.IsWellFormed(out failureReason);
    }
}