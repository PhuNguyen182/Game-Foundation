using Object = UnityEngine.Object;

namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>What a source produced.</summary>
    public enum StaticDataPayloadKind
    {
        /// <summary>The source had nothing for this key. Not an error — the chain falls through.</summary>
        Missing = 0,

        /// <summary>Raw text: remote config value, downloaded CSV, a <c>TextAsset</c>'s contents.</summary>
        Text = 1,

        /// <summary>A loaded Unity asset, normally a <see cref="UnityEngine.ScriptableObject"/>.</summary>
        Asset = 2,
    }

    /// <summary>
    /// One source's answer for one key, plus whatever that source needs in order to release it later.
    /// </summary>
    /// <remarks>
    /// <para><b>The release handle travels with the payload.</b> The previous design kept a single
    /// <c>_dataProvider</c> field on the controller and overwrote it on every iteration of the
    /// fallback loop, so cleanup ran against whichever provider happened to be built last rather
    /// than the one that actually loaded the data — asking Addressables to release an object that
    /// came from <c>Resources.Load</c>. Carrying the handle here makes that class of mistake
    /// unrepresentable: the source that produced a payload is the only thing that releases it, and
    /// it gets back exactly what it handed out.</para>
    ///
    /// <para>A <see cref="StaticDataPayloadKind.Text"/> payload normally needs no release at all;
    /// the field is still here because a future source may hand back a pooled buffer.</para>
    /// </remarks>
    public readonly struct StaticDataPayload
    {
        private StaticDataPayload(StaticDataPayloadKind kind, string text, Object asset, object releaseHandle)
        {
            this.Kind = kind;
            this.Text = text;
            this.Asset = asset;
            this.ReleaseHandle = releaseHandle;
        }

        public StaticDataPayloadKind Kind { get; }

        /// <summary>Set when <see cref="Kind"/> is <see cref="StaticDataPayloadKind.Text"/>.</summary>
        public string Text { get; }

        /// <summary>Set when <see cref="Kind"/> is <see cref="StaticDataPayloadKind.Asset"/>.</summary>
        public Object Asset { get; }

        /// <summary>
        /// Opaque to everyone but the source that created it — an Addressables operation handle, for
        /// instance. Never inspected outside <see cref="IStaticDataSource.Release"/>.
        /// </summary>
        public object ReleaseHandle { get; }

        public bool HasValue => this.Kind != StaticDataPayloadKind.Missing;

        /// <summary>The source has nothing for this key. The chain moves on to the next source.</summary>
        public static StaticDataPayload Missing() =>
            new(StaticDataPayloadKind.Missing, null, null, null);

        public static StaticDataPayload FromText(string text, object releaseHandle = null) =>
            string.IsNullOrEmpty(text)
                ? Missing()
                : new StaticDataPayload(StaticDataPayloadKind.Text, text, null, releaseHandle);

        public static StaticDataPayload FromAsset(Object asset, object releaseHandle = null) =>
            asset == null
                ? Missing()
                : new StaticDataPayload(StaticDataPayloadKind.Asset, null, asset, releaseHandle);
    }
}
