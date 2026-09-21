namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>
    /// The only thing static data needs from a remote config service: is it ready, and what string
    /// is under this key.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a port rather than using <c>IRemoteConfigService</c> directly.</b> That interface
    /// lives in the game assembly alongside a Firebase implementation, and it exposes typed getters
    /// this layer never calls. Depending on it would (a) tie the foundation's config loader to one
    /// vendor and (b) make it impossible to move DataFlow into its own assembly definition, since an
    /// asmdef cannot reference <c>Assembly-CSharp</c>. Two members here, an adapter on the game
    /// side, and both problems are gone.</para>
    ///
    /// <para><see cref="IsReady"/> must report the service's <i>actual</i> state. A service that
    /// flips the flag in a <c>finally</c> block after a failed init is worse than one that stays
    /// false: the reader then hands back empty strings that look like "key not configured", and the
    /// chain falls through to local data believing remote simply had nothing to say.</para>
    /// </remarks>
    public interface IStaticRemoteConfigReader
    {
        /// <summary>True once values can be read. Polled with a timeout, never awaited forever.</summary>
        bool IsReady { get; }

        /// <summary>
        /// The raw value under <paramref name="key"/>, or an empty string when the key is not set.
        /// An empty result is a miss, not an error.
        /// </summary>
        string GetString(string key);
    }
}
