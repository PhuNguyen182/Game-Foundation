using System;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>One channel's mix settings, as plain data.</summary>
    [Serializable]
    public struct AudioChannelVolume
    {
        public string ChannelId;
        public float Linear;
        public bool Muted;
    }

    /// <summary>
    /// Every channel's mix settings, for a game to save and restore.
    /// </summary>
    /// <remarks>
    /// <para><b>The audio service does not persist this itself, on purpose.</b> This project already
    /// owns a save system with domain ids, schema versions and a migration pipeline. A service that
    /// quietly reached for <c>PlayerPrefs</c> would create a second source of truth that no
    /// migration knows about and that the settings screen cannot take part in.</para>
    ///
    /// <para>So the game layer owns a data controller for this and calls
    /// <c>ApplyVolumeSnapshot</c> once its save has loaded. "Why doesn't my volume save" is the
    /// predictable first question, and this is the answer.</para>
    /// </remarks>
    [Serializable]
    public struct AudioVolumeSnapshot
    {
        public AudioChannelVolume[] Channels;
    }
}
