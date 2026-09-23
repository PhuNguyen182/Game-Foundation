using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Interfaces;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using UnityEngine.Audio;

namespace DracoRuan.PrebuildServices.AudioSystem.Core.Mixing
{
    /// <summary>
    /// Owns the channel table and every write to the <c>AudioMixer</c>.
    /// </summary>
    /// <remarks>
    /// <para>The master channel is an ordinary entry in the list. Nothing here knows the words
    /// Music, SFX or UI, which is what lets the number and naming of channels be a property of the
    /// game rather than of this code.</para>
    /// </remarks>
    public sealed class AudioMixerController : IAudioMixerController
    {
        private const string LogTag = AudioConstants.LogTag;

        private readonly AudioMixer _mixer;
        private readonly AudioChannelRuntime[] _channels;
        private readonly Dictionary<string, int> _indexById;
        private readonly string[] _channelIds;
        private readonly HashSet<string> _warnedParameters = new HashSet<string>(StringComparer.Ordinal);

        public AudioMixerController(AudioMixer mixer, IReadOnlyList<AudioChannelDefinition> definitions)
        {
            this._mixer = mixer;
            this._channels = new AudioChannelRuntime[definitions?.Count ?? 0];
            this._channelIds = new string[this._channels.Length];
            this._indexById = new Dictionary<string, int>(this._channels.Length, StringComparer.Ordinal);

            for (int i = 0; i < this._channels.Length; i++)
            {
                AudioChannelDefinition definition = definitions[i];
                AudioChannelRuntime channel = new AudioChannelRuntime(definition, i);

                this._channels[i] = channel;
                this._channelIds[i] = channel.Id;

                if (string.IsNullOrEmpty(channel.Id))
                {
                    Debug.LogError($"[{LogTag}] Channel {i} in the audio config has no id. "
                                   + "Nothing can be routed to it.");
                    continue;
                }

                if (this._indexById.ContainsKey(channel.Id))
                {
                    Debug.LogError($"[{LogTag}] Channel id '{channel.Id}' is declared twice in the audio "
                                   + "config. The second one is ignored.");
                    continue;
                }

                this._indexById.Add(channel.Id, i);
            }
        }

        public IReadOnlyList<string> ChannelIds => this._channelIds;

        /// <summary>The channel table, for the service's own bookkeeping.</summary>
        public IReadOnlyList<AudioChannelRuntime> Channels => this._channels;

        public bool TryGetChannelIndex(string channelId, out int index)
        {
            if (channelId != null && this._indexById.TryGetValue(channelId, out index))
                return true;

            index = -1;
            return false;
        }

        public AudioChannelRuntime GetChannel(int channelIndex) =>
            this.IsValidIndex(channelIndex) ? this._channels[channelIndex] : null;

        public AudioMixerGroup GetMixerGroup(int channelIndex) =>
            this.IsValidIndex(channelIndex) ? this._channels[channelIndex].MixerGroup : null;

        public float GetPerVoicePitch(int channelIndex)
        {
            if (!this.IsValidIndex(channelIndex))
                return 1f;

            AudioChannelRuntime channel = this._channels[channelIndex];

            // Mixer-driven channels return 1 so callers can multiply unconditionally.
            return channel.PitchMode == AudioChannelPitchMode.PerVoice ? channel.Pitch : 1f;
        }

        public void SetChannelVolume(int channelIndex, float linear01)
        {
            if (!this.IsValidIndex(channelIndex))
                return;

            AudioChannelRuntime channel = this._channels[channelIndex];
            float clamped = linear01 < 0f ? 0f : linear01 > 1f ? 1f : linear01;

            channel.LinearVolume = clamped;

            if (!channel.Muted)
            {
                channel.PreMuteVolume = clamped;
                this.WriteVolume(channel, clamped);
            }
        }

        public float GetChannelVolume(int channelIndex) =>
            this.IsValidIndex(channelIndex) ? this._channels[channelIndex].LinearVolume : 0f;

        public void SetChannelPitch(int channelIndex, float pitch)
        {
            if (!this.IsValidIndex(channelIndex))
                return;

            AudioChannelRuntime channel = this._channels[channelIndex];
            channel.Pitch = pitch;

            if (channel.PitchMode == AudioChannelPitchMode.MixerParameter)
                this.WriteParameter(channel.PitchParameter, pitch);

            // In PerVoice mode the service re-applies the pitch to live voices; there is nothing to
            // write to the mixer.
        }

        public float GetChannelPitch(int channelIndex) =>
            this.IsValidIndex(channelIndex) ? this._channels[channelIndex].Pitch : 1f;

        public void SetChannelMuted(int channelIndex, bool muted)
        {
            if (!this.IsValidIndex(channelIndex))
                return;

            AudioChannelRuntime channel = this._channels[channelIndex];
            if (channel.Muted == muted)
                return;

            channel.Muted = muted;

            if (muted)
            {
                channel.PreMuteVolume = channel.LinearVolume;
                this.WriteVolume(channel, 0f);
                return;
            }

            channel.LinearVolume = channel.PreMuteVolume;
            this.WriteVolume(channel, channel.PreMuteVolume);
        }

        public bool IsChannelMuted(int channelIndex) =>
            this.IsValidIndex(channelIndex) && this._channels[channelIndex].Muted;

        public void ApplyDefaults()
        {
            for (int i = 0; i < this._channels.Length; i++)
            {
                AudioChannelRuntime channel = this._channels[i];

                channel.Muted = false;
                channel.LinearVolume = channel.DefaultVolume;
                channel.PreMuteVolume = channel.DefaultVolume;
                channel.Pitch = 1f;

                this.WriteVolume(channel, channel.DefaultVolume);

                if (channel.PitchMode == AudioChannelPitchMode.MixerParameter)
                    this.WriteParameter(channel.PitchParameter, 1f);
            }
        }

        public AudioVolumeSnapshot GetSnapshot()
        {
            AudioChannelVolume[] volumes = new AudioChannelVolume[this._channels.Length];

            for (int i = 0; i < this._channels.Length; i++)
                volumes[i] = new AudioChannelVolume
                {
                    channelId = this._channels[i].Id,
                    linear = this._channels[i].Muted ? this._channels[i].PreMuteVolume : this._channels[i].LinearVolume,
                    muted = this._channels[i].Muted,
                };

            return new AudioVolumeSnapshot { channels = volumes };
        }

        public void ApplySnapshot(in AudioVolumeSnapshot snapshot)
        {
            if (snapshot.channels == null)
                return;

            for (int i = 0; i < snapshot.channels.Length; i++)
            {
                AudioChannelVolume saved = snapshot.channels[i];

                // A build that dropped a channel must still load an older save.
                if (!this.TryGetChannelIndex(saved.channelId, out int index))
                    continue;

                this.SetChannelMuted(index, false);
                this.SetChannelVolume(index, saved.linear);
                this.SetChannelMuted(index, saved.muted);
            }
        }

        private void WriteVolume(AudioChannelRuntime channel, float linear) =>
            this.WriteParameter(channel.VolumeParameter, AudioDecibels.LinearToDecibels(linear));

        /// <remarks>
        /// <c>SetFloat</c> returns false and does nothing at all when the parameter was never
        /// exposed in the mixer. That is the single most common silent failure in Unity audio, so it
        /// is reported — once per parameter name, because a volume slider would otherwise produce
        /// one error per frame while it is being dragged.
        /// </remarks>
        private void WriteParameter(string parameterName, float value)
        {
            if (this._mixer == null || string.IsNullOrEmpty(parameterName))
                return;

            if (this._mixer.SetFloat(parameterName, value))
                return;

            if (this._warnedParameters.Add(parameterName))
                Debug.LogError($"[{LogTag}] The mixer has no exposed parameter named '{parameterName}'. "
                               + "Expose it in the Audio Mixer window (right-click the value, "
                               + "\"Expose ... to script\") or fix the name in the audio config. "
                               + "Until then this channel cannot be controlled from code.");
        }

        private bool IsValidIndex(int channelIndex) =>
            channelIndex >= 0 && channelIndex < this._channels.Length;
    }
}
