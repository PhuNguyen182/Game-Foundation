using System;

namespace DracoRuan.Foundation.DataFlow.LocalData
{
    /// <summary>
    /// Marks a save repository and records the domain id its data is stored under.
    /// </summary>
    /// <remarks>
    /// <para>Lets Editor tooling map a controller type to its save files without constructing the
    /// controller - which it could not do anyway, since that needs the container, and which would
    /// make it read the very data the tool is about to edit.</para>
    ///
    /// <para><b>Pass the controller's own <c>Id</c> constant, not a literal</b>, so the attribute and
    /// the <c>DomainId</c> property cannot drift apart:</para>
    /// <code>
    /// [DynamicGameDataController(RiseProgressionDataController.Id)]
    /// public sealed class RiseProgressionDataController : DynamicGameDataController&lt;RiseProgressDataV1&gt;
    /// {
    ///     public const string Id = "rise_progression";
    ///     public override string DomainId =&gt; Id;
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DynamicGameDataControllerAttribute : Attribute
    {
        public DynamicGameDataControllerAttribute(string domainId)
        {
            this.DomainId = domainId;
        }

        /// <summary>Domain id this controller's data is stored under.</summary>
        public string DomainId { get; }
    }
}
