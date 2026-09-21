using System;

namespace DracoRuan.Foundation.DataFlow.StaticData
{
    /// <summary>
    /// Marks a static data controller and records the id its table is known by.
    /// </summary>
    /// <remarks>
    /// <para>Lets tooling map a controller type to its data without constructing one — which it could
    /// not do anyway, since that needs the container, and which would make the tool run the very
    /// fallback chain it is trying to inspect.</para>
    ///
    /// <para><b>Pass the controller's own <c>Id</c> constant, not a literal</b>, so the attribute and
    /// the <c>DataId</c> property cannot drift apart:</para>
    /// <code>
    /// [StaticDataId(GachaRateController.Id)]
    /// public sealed class GachaRateController : StaticDataController&lt;GachaRateData&gt;
    /// {
    ///     public const string Id = "gacha_rate";
    ///     public override string DataId =&gt; Id;
    /// }
    /// </code>
    ///
    /// <para>Named for the id rather than for the controller so that <c>[StaticDataController(...)]</c>
    /// does not read as the base class <c>StaticDataController&lt;T&gt;</c> one line below it.</para>
    ///
    /// <para>The attribute it replaces, <c>StaticGameDataControllerAttribute</c>, was declared and then
    /// read by nothing at all — no registry, no reflection scan, no tool. Registration checks this one,
    /// so a controller that forgets it fails at container build time rather than never.</para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StaticDataIdAttribute : Attribute
    {
        public StaticDataIdAttribute(string dataId)
        {
            this.DataId = dataId;
        }

        /// <summary>Permanent id of the table this controller owns.</summary>
        public string DataId { get; }
    }
}
