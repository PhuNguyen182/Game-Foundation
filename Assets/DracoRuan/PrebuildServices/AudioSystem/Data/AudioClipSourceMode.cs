namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>Where an entry's audio data comes from.</summary>
    /// <remarks>
    /// Both exist because the right answer differs by sound. A short UI click wants to be resident
    /// and play synchronously; a three-minute music bed has no business sitting in memory from
    /// boot. Forcing either choice on every entry makes one of the two cases wrong.
    /// </remarks>
    public enum AudioClipSourceMode
    {
        /// <summary>A direct <c>AudioClip</c> reference. Resident, and plays with no await.</summary>
        Direct = 0,

        /// <summary>An Addressables reference, loaded on demand and released when idle.</summary>
        AssetReference = 1,
    }
}
