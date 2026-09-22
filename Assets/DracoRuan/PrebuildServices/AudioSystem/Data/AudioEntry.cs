using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data.Attributes;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using Sirenix.OdinInspector;
using UnityEngine;
#if USE_EXTENDED_ADDRESSABLE
using UnityEngine.AddressableAssets;
#endif

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// One authored sound: what to play, how loud, on which channel, and how often it may repeat.
    /// </summary>
    /// <remarks>
    /// <para>Plain Unity serialization, not Odin serialization, so the <c>.asset</c> stays readable
    /// YAML that merges in version control. Odin is used only for inspector attributes.</para>
    ///
    /// <para><b>Nothing here is mutated at runtime.</b> Play timestamps and live instance counts
    /// live in the service, because state written onto a ScriptableObject survives leaving play
    /// mode in the Editor and would make the second play session of the day behave differently
    /// from the first.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "AudioEntry", menuName = "DracoRuan/AudioSystem/AudioEntry")]
    public class AudioEntry : ScriptableObject
    {
        [Title("Identity")]
        [Tooltip("Unique name. Generated into the AudioId class, so it is also a C# member name.")]
        [SerializeField]
        private string _id;

        [AudioChannelId] [Tooltip("Which mixer channel this plays on.")] [SerializeField]
        private string _channelId;

        [Title("Clip")] [SerializeField] private AudioClipSourceMode _clipMode = AudioClipSourceMode.Direct;

        [ShowIf(nameof(_clipMode), AudioClipSourceMode.Direct)] [SerializeField]
        private AudioClip _clip;

        [ShowIf(nameof(_clipMode), AudioClipSourceMode.Direct)]
        [Tooltip("Optional. When filled in, each play picks one of these instead of the clip above, "
                 + "never the same one twice in a row.")]
        [SerializeField]
        private List<AudioClip> _clipVariants = new List<AudioClip>();

#if USE_EXTENDED_ADDRESSABLE
        [ShowIf(nameof(_clipMode), AudioClipSourceMode.AssetReference)] [SerializeField]
        private AssetReferenceT<AudioClip> _clipReference;
#endif

        [Tooltip("Load this clip during boot and keep it resident.")] [SerializeField]
        private bool _preload;

        [Title("Mix")] [SerializeField] private bool _loop;

        [Range(0f, 1f)] [SerializeField] private float _volume = 1f;

        [Tooltip("Min and max. Leave both at 0 to always use the volume above.")]
        [MinMaxSlider(0f, 1f, true)]
        [SerializeField]
        private Vector2 _volumeRandomRange;

        [Range(0.1f, 3f)] [SerializeField] private float _pitch = 1f;

        [Tooltip("Min and max. Leave both at 0 to always use the pitch above.")]
        [MinMaxSlider(0f, 3f, true)]
        [SerializeField]
        private Vector2 _pitchRandomRange;

        [Tooltip("0 is most important. Unity virtualises the least important voices first.")]
        [Range(0, 256)]
        [SerializeField]
        private int _priority = 128;

        [Range(-1f, 1f)] [SerializeField] private float _stereoPan;

        [Title("Space")] [SerializeField] private Audio3DSettings _settings3D = new Audio3DSettings();

        [Title("Throttling")]
        [Tooltip("Shortest gap between two plays of this entry. 0 disables the check.")]
        [MinValue(0f)]
        [SerializeField]
        private float _minIntervalSeconds;

        [Tooltip("How many instances may sound at once. 0 means no limit.")] [MinValue(0)] [SerializeField]
        private int _maxConcurrent;

        [SerializeField] private AudioConcurrencyPolicy _concurrencyPolicy = AudioConcurrencyPolicy.DropNewest;

        public string Id => this._id;
        public string ChannelId => this._channelId;
        public AudioClipSourceMode ClipMode => this._clipMode;
        public AudioClip Clip => this._clip;
        public IReadOnlyList<AudioClip> ClipVariants => this._clipVariants;
        public bool Preload => this._preload;
        public bool Loop => this._loop;
        public float Volume => this._volume;
        public float Pitch => this._pitch;
        public int Priority => this._priority;
        public float StereoPan => this._stereoPan;
        public Audio3DSettings Settings3D => this._settings3D;

#if USE_EXTENDED_ADDRESSABLE
        public AssetReferenceT<AudioClip> ClipReference => this._clipReference;
#endif

        /// <summary>This entry's throttling rule, as the gate wants it.</summary>
        public AudioFireRateRule FireRateRule =>
            new AudioFireRateRule(this._minIntervalSeconds, this._maxConcurrent, this._concurrencyPolicy);

        /// <summary>Whether a play should choose between several clips.</summary>
        public bool HasVariants => this._clipVariants != null && this._clipVariants.Count > 1;

        /// <summary>
        /// The clip to play this time, avoiding an immediate repeat when variants are authored.
        /// </summary>
        /// <param name="lastVariantIndex">
        /// Caller-owned memory of the previous pick, updated in place. The service keeps one of
        /// these per entry; it is not stored on the asset.
        /// </param>
        public AudioClip SelectClip(ref int lastVariantIndex, float roll01)
        {
            if (!this.HasVariants)
                return this._clip;

            int index = AudioClipVariantSelector.Next(this._clipVariants.Count, lastVariantIndex, roll01);
            lastVariantIndex = index;

            // A hole in the variant list should not silence the entry.
            return this._clipVariants[index] != null ? this._clipVariants[index] : this._clip;
        }

        /// <summary>The volume for this play, after any authored randomisation.</summary>
        public float ResolveVolume(float roll01) =>
            AudioValueRange.Resolve(this._volume, this._volumeRandomRange.x, this._volumeRandomRange.y, roll01);

        /// <summary>The pitch for this play, after any authored randomisation.</summary>
        public float ResolvePitch(float roll01) =>
            AudioValueRange.Resolve(this._pitch, this._pitchRandomRange.x, this._pitchRandomRange.y, roll01);

        /// <summary>
        /// Whether this entry can produce sound at all, and if not, why. Used by the editor tool and
        /// by the service's startup validation.
        /// </summary>
        public bool IsPlayable(out string reason)
        {
            if (this._clipMode == AudioClipSourceMode.Direct)
            {
                if (this._clip == null && !this.HasVariants)
                {
                    reason = $"Audio entry '{this._id}' has no clip assigned.";
                    return false;
                }

                reason = null;
                return true;
            }

#if USE_EXTENDED_ADDRESSABLE
            if (this._clipReference == null || !this._clipReference.RuntimeKeyIsValid())
            {
                reason = $"Audio entry '{this._id}' is set to Addressables but has no asset assigned.";
                return false;
            }

            reason = null;
            return true;
#else
            reason = $"Audio entry '{this._id}' needs Addressables, but USE_EXTENDED_ADDRESSABLE is "
                     + "not defined in this build.";
            return false;
#endif
        }
    }
}