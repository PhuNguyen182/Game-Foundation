using Newtonsoft.Json;

namespace DracoRuan.Foundation.DataFlow.StaticData.Decoders
{
    /// <summary>
    /// The JSON settings every static data decode goes through.
    /// </summary>
    /// <remarks>
    /// <para>Non-generic on purpose. Held as a static on a generic decoder, these settings would be
    /// duplicated once per closed type and each copy would build its own contract cache — so a
    /// project with forty config tables would reflect over the same base types forty times.</para>
    /// </remarks>
    public static class StaticDataJson
    {
        /// <summary>
        /// <para><see cref="UnitySerializationContractResolver"/> is what makes private
        /// <c>[SerializeField]</c> members visible; without it a config asset populates to nothing.</para>
        ///
        /// <para><see cref="MissingMemberHandling.Error"/> turns a remote payload whose shape has
        /// drifted from this build into a failed decode, so the chain falls back to the shipped asset
        /// instead of loading a half-applied config and reporting success.</para>
        ///
        /// <para><see cref="ObjectCreationHandling.Replace"/> matters for collections: the default
        /// would <i>append</i> the payload's entries to whatever the ScriptableObject already held,
        /// so a remote list of five rows would come out as the authored rows plus five.</para>
        /// </summary>
        public static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = UnitySerializationContractResolver.Instance,
            MissingMemberHandling = MissingMemberHandling.Error,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };
    }
}
