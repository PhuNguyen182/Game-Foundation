using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using UnityEngine.Audio;

namespace DracoRuan.PrebuildServices.AudioSystem.Interfaces
{
    /// <summary>
    /// Maps channel names onto mixer groups and exposed parameters.
    /// </summary>
    /// <remarks>
    /// <c>AudioMixer.SetFloat</c> returns false and does nothing when the parameter was never
    /// exposed, which is the most common silent failure in Unity audio. Every write here checks
    /// that result and reports it once per parameter name.
    /// </remarks>
    public interface IAudioMixerController
    {
        public IReadOnlyList<string> ChannelIds { get; }

        public bool TryGetChannelIndex(string channelId, out int index);

        public AudioMixerGroup GetMixerGroup(int channelIndex);

        /// <summary>The pitch multiplier voices on this channel must apply themselves.</summary>
        /// <remarks>
        /// Always 1 for a channel whose pitch is driven through the mixer, so a caller can multiply
        /// unconditionally without asking which mode the channel uses.
        /// </remarks>
        public float GetPerVoicePitch(int channelIndex);

        public void SetChannelVolume(int channelIndex, float linear01);

        public float GetChannelVolume(int channelIndex);

        public void SetChannelPitch(int channelIndex, float pitch);

        public float GetChannelPitch(int channelIndex);

        public void SetChannelMuted(int channelIndex, bool muted);

        public bool IsChannelMuted(int channelIndex);

        /// <summary>Writes every channel's authored default to the mixer.</summary>
        /// <remarks>
        /// Not optional. Exposed parameter values are saved into the mixer asset while you work in
        /// the Editor, so without this a build sounds different from the Editor it was made in.
        /// </remarks>
        public void ApplyDefaults();

        public AudioVolumeSnapshot GetSnapshot();

        public void ApplySnapshot(in AudioVolumeSnapshot snapshot);
    }
}
