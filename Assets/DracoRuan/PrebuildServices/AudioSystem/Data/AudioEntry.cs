using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data.Attributes;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("_id")]
        [Title("Identity")]
        [Tooltip("Unique name. Generated into the AudioId class, so it is also a C# member name.")]
        [SerializeField]
        private string id;

        [FormerlySerializedAs("_channelId")] [AudioChannelId] [Tooltip("Which mixer channel this plays on.")] [SerializeField]
        private string channelId;

        [Title("Clip")] [SerializeField] private AudioClipSourceMode _clipMode = AudioClipSourceMode.Direct;

        [FormerlySerializedAs("_clip")] [ShowIf(nameof(_clipMode), AudioClipSourceMode.Direct)] [SerializeField]
        private AudioClip clip;

        [FormerlySerializedAs("_clipVariants")]
        [ShowIf(nameof(_clipMode), AudioClipSourceMode.Direct)]
        [Tooltip("Optional. When filled in, each play picks one of these instead of the clip above, "
                 + "never the same one twice in a row.")]
        [SerializeField]
        private List<AudioClip> clipVariants = new();

#if USE_EXTENDED_ADDRESSABLE
        [FormerlySerializedAs("_clipReference")] [ShowIf(nameof(_clipMode), AudioClipSourceMode.AssetReference)] [SerializeField]
        private AssetReferenceT<AudioClip> clipReference;
#endif

        [FormerlySerializedAs("_preload")] [Tooltip("Load this clip during boot and keep it resident.")] [SerializeField]
        private bool preload;

        [FormerlySerializedAs("_loop")] [Title("Mix")] [SerializeField] private bool loop;

        [FormerlySerializedAs("_volume")] [Range(0f, 1f)] [SerializeField] private float volume = 1f;

        [FormerlySerializedAs("_volumeRandomRange")]
        [Tooltip("Min and max. Leave both at 0 to always use the volume above.")]
        [MinMaxSlider(0f, 1f, true)]
        [SerializeField]
        private Vector2 volumeRandomRange;

        [FormerlySerializedAs("_pitch")] [Range(0.1f, 3f)] [SerializeField] private float pitch = 1f;

        [FormerlySerializedAs("_pitchRandomRange")]
        [Tooltip("Min and max. Leave both at 0 to always use the pitch above.")]
        [MinMaxSlider(0f, 3f, true)]
        [SerializeField]
        private Vector2 pitchRandomRange;

        [FormerlySerializedAs("_priority")]
        [Tooltip("0 is most important. Unity virtualises the least important voices first.")]
        [Range(0, 256)]
        [SerializeField]
        private int priority = 128;

        [FormerlySerializedAs("_stereoPan")] [Range(-1f, 1f)] [SerializeField] private float stereoPan;

        [FormerlySerializedAs("_settings3D")] [Title("Space")] [SerializeField]
        private Audio3DSettings settings3D = new();

        [FormerlySerializedAs("_minIntervalSeconds")]
        [Title("Throttling")]
        [Tooltip("Shortest gap between two plays of this entry. 0 disables the check.")]
        [MinValue(0f)]
        [SerializeField]
        private float minIntervalSeconds;

        [FormerlySerializedAs("_maxConcurrent")]
        [Tooltip("How many instances may sound at once. 0 means no limit.")]
        [MinValue(0)]
        [SerializeField]
        private int maxConcurrent;

        [FormerlySerializedAs("_concurrencyPolicy")] [SerializeField]
        private AudioConcurrencyPolicy concurrencyPolicy = AudioConcurrencyPolicy.DropNewest;

        public string Id => this.id;
        public string ChannelId => this.channelId;
        public AudioClipSourceMode ClipMode => this._clipMode;
        public AudioClip Clip => this.clip;
        public IReadOnlyList<AudioClip> ClipVariants => this.clipVariants;
        public bool Preload => this.preload;
        public bool Loop => this.loop;
        public float Volume => this.volume;
        public float Pitch => this.pitch;
        public int Priority => this.priority;
        public float StereoPan => this.stereoPan;
        public Audio3DSettings Settings3D => this.settings3D;

#if USE_EXTENDED_ADDRESSABLE
        public AssetReferenceT<AudioClip> ClipReference => this.clipReference;
#endif

        /// <summary>This entry's throttling rule, as the gate wants it.</summary>
        public AudioFireRateRule FireRateRule =>
            new AudioFireRateRule(this.minIntervalSeconds, this.maxConcurrent, this.concurrencyPolicy);

        /// <summary>Whether a play should choose between several clips.</summary>
        public bool HasVariants => this.clipVariants != null && this.clipVariants.Count > 1;

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
                return this.clip;

            int index = AudioClipVariantSelector.Next(this.clipVariants.Count, lastVariantIndex, roll01);
            lastVariantIndex = index;

            // A hole in the variant list should not silence the entry.
            return this.clipVariants[index] ? this.clipVariants[index] : this.clip;
        }

        /// <summary>The volume for this play, after any authored randomisation.</summary>
        public float ResolveVolume(float roll01) =>
            AudioValueRange.Resolve(this.volume, this.volumeRandomRange.x, this.volumeRandomRange.y, roll01);

        /// <summary>The pitch for this play, after any authored randomisation.</summary>
        public float ResolvePitch(float roll01) =>
            AudioValueRange.Resolve(this.pitch, this.pitchRandomRange.x, this.pitchRandomRange.y, roll01);

        /// <summary>
        /// Whether this entry can produce sound at all, and if not, why. Used by the editor tool and
        /// by the service's startup validation.
        /// </summary>
        public bool IsPlayable(out string reason)
        {
            if (this._clipMode == AudioClipSourceMode.Direct)
            {
                if (!this.clip && !this.HasVariants)
                {
                    reason = $"Audio entry '{this.id}' has no clip assigned.";
                    return false;
                }

                reason = null;
                return true;
            }

#if USE_EXTENDED_ADDRESSABLE
            if (this.clipReference == null || !this.clipReference.RuntimeKeyIsValid())
            {
                reason = $"Audio entry '{this.id}' is set to Addressables but has no asset assigned.";
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