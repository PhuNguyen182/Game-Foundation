namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>
    /// Where a static data table can be read from.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately not a <c>[Flags]</c> enum. A fallback chain is <i>ordered</i> — "remote
    /// first, then the shipped asset" is a different chain from the reverse — and a bit field cannot
    /// express order. Controllers declare an ordered array instead.</para>
    ///
    /// <para><b>PlayerPrefs and File are gone on purpose.</b> They were save-data concepts leaking
    /// into config loading; the dynamic half owns persistence now (see <c>SaveEnvelopeStore</c>).
    /// Static data is read-only reference data and never writes anywhere.</para>
    /// </remarks>
    public enum StaticDataSourceType
    {
        None = 0,

        /// <summary>Remote config key holding a JSON or CSV payload as a string.</summary>
        RemoteConfig = 1,

        /// <summary>Path under a <c>Resources</c> folder.</summary>
        Resources = 2,

        /// <summary>Addressables address or label.</summary>
        Addressable = 3,

        /// <summary>Direct URL — R2, S3 or any CDN. Downloaded, then cached on disk.</summary>
        Url = 4,
    }
}
