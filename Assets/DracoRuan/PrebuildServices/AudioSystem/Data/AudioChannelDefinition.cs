using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// One mixer channel, as authored in <see cref="AudioConfig"/>.
    /// </summary>
    /// <remarks>
    /// Channels are data rather than an enum because how many a game needs, and what they are
    /// called, is a property of the game. Nothing in the service hard-codes Music, SFX or UI; even
    /// the master channel is an ordinary entry in this list.
    /// </remarks>
    [Serializable]
    public class AudioChannelDefinition
    {
        [Tooltip("Name used in code and on entries. Same character rules as an audio id.")] [SerializeField]
        private string _id;

        [Tooltip("The mixer group voices on this channel route into.")] [SerializeField]
        private AudioMixerGroup _mixerGroup;

        [Tooltip("The mixer's exposed parameter for this group's volume, in decibels.")] [SerializeField]
        private string _volumeParameter;

        [SerializeField] private AudioChannelPitchMode _pitchMode = AudioChannelPitchMode.PerVoice;

        [ShowIf(nameof(_pitchMode), AudioChannelPitchMode.MixerParameter)]
        [Tooltip("The mixer's exposed parameter for this group's pitch.")]
        [SerializeField]
        private string _pitchParameter;

        [Range(0f, 1f)]
        [Tooltip("Applied at startup. Editor mixer values persist between sessions, so this is what "
                 + "makes a build sound like the Editor did.")]
        [SerializeField]
        private float _defaultVolume = 1f;

        [Tooltip("Voices created up front so this channel never waits to be heard. Give a music "
                 + "channel at least 2 so a cross-fade always has room.")]
        [MinValue(0)]
        [SerializeField]
        private int _reservedVoices;

        [Tooltip("Hardest limit on simultaneous voices for this channel. 0 means no limit.")]
        [MinValue(0)]
        [SerializeField]
        private int _maxVoices;

        public string Id => this._id;
        public AudioMixerGroup MixerGroup => this._mixerGroup;
        public string VolumeParameter => this._volumeParameter;
        public string PitchParameter => this._pitchParameter;
        public AudioChannelPitchMode PitchMode => this._pitchMode;
        public float DefaultVolume => this._defaultVolume;
        public int ReservedVoices => this._reservedVoices;
        public int MaxVoices => this._maxVoices;
    }
}