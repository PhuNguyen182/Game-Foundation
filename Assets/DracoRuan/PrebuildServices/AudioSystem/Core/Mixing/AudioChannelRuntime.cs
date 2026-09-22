using DracoRuan.PrebuildServices.AudioSystem.Data;
using UnityEngine.Audio;

namespace DracoRuan.PrebuildServices.AudioSystem.Core.Mixing
{
    /// <summary>The live state of one channel, built from its authored definition.</summary>
    public sealed class AudioChannelRuntime
    {
        public AudioChannelRuntime(AudioChannelDefinition definition, int index)
        {
            this.Id = definition.Id;
            this.Index = index;
            this.MixerGroup = definition.MixerGroup;
            this.VolumeParameter = definition.VolumeParameter;
            this.PitchParameter = definition.PitchParameter;
            this.PitchMode = definition.PitchMode;
            this.DefaultVolume = definition.DefaultVolume;
            this.ReservedVoices = definition.ReservedVoices;
            this.MaxVoices = definition.MaxVoices;

            this.LinearVolume = definition.DefaultVolume;
            this.PreMuteVolume = definition.DefaultVolume;
            this.Pitch = 1f;
        }

        public string Id { get; }
        public int Index { get; }
        public AudioMixerGroup MixerGroup { get; }
        public string VolumeParameter { get; }
        public string PitchParameter { get; }
        public AudioChannelPitchMode PitchMode { get; }
        public float DefaultVolume { get; }
        public int ReservedVoices { get; }
        public int MaxVoices { get; }

        public float LinearVolume { get; set; }
        public float Pitch { get; set; }
        public bool Muted { get; set; }

        /// <summary>The volume to restore on unmute, so muting is not destructive.</summary>
        public float PreMuteVolume { get; set; }

        /// <summary>
        /// Whether this channel is paused. A voice started while it is set arrives paused, so a
        /// sound triggered from a pause menu does not leak through.
        /// </summary>
        public bool Paused { get; set; }
    }
}
