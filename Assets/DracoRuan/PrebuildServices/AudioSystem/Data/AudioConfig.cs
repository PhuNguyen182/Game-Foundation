using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// Everything about the audio system that is a property of the game rather than of one sound.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "DracoRuan/AudioSystem/AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [FormerlySerializedAs("_mixer")] [Title("Mixer")] [Required] [SerializeField]
        private AudioMixer mixer;

        [FormerlySerializedAs("_channels")]
        [Tooltip("Every channel this game has. Order does not matter.")]
        [ListDrawerSettings(ShowFoldout = true)]
        [SerializeField]
        private List<AudioChannelDefinition> channels = new();

        [FormerlySerializedAs("_masterChannelId")]
        [Tooltip("Which channel stands for the mixer's master bus. Nothing special-cases it beyond "
                 + "SetMasterVolume routing here.")]
        [SerializeField]
        private string masterChannelId = AudioConstants.DefaultMasterChannelId;

        [FormerlySerializedAs("_maxTotalVoices")]
        [Title("Voices")]
        [Tooltip("Ceiling on simultaneous voices. Mobile hardware manages roughly 24 to 32 before "
                 + "Unity starts virtualising by priority on its own.")]
        [MinValue(1)]
        [SerializeField]
        private int maxTotalVoices = AudioConstants.DefaultMaxTotalVoices;

        [FormerlySerializedAs("_useUnscaledTime")]
        [Title("Timing")]
        [Tooltip("Run fades on unscaled time. AudioSource playback ignores timeScale, so a fade that "
                 + "froze at timeScale 0 would be the odd one out.")]
        [SerializeField]
        private bool useUnscaledTime = true;

        [FormerlySerializedAs("_maxTickDeltaSeconds")]
        [Tooltip("Largest delta a single tick will act on. Guards the first frame after the app "
                 + "returns from the background, where delta time can be tens of seconds.")]
        [MinValue(0.01f)]
        [SerializeField]
        private float maxTickDeltaSeconds = AudioConstants.DefaultMaxTickDeltaSeconds;

        [FormerlySerializedAs("_clipUnloadGraceSeconds")]
        [Title("Loading")]
        [Tooltip("How long an Addressables clip stays resident after its last voice releases it.")]
        [MinValue(0f)]
        [SerializeField]
        private float clipUnloadGraceSeconds = AudioConstants.DefaultClipUnloadGraceSeconds;

        [FormerlySerializedAs("_preloadTimeoutSeconds")]
        [Tooltip("Per-entry budget for a preload, so a stall names the clip instead of timing out "
                 + "the whole boot pipeline with nothing to go on.")]
        [MinValue(1f)]
        [SerializeField]
        private float preloadTimeoutSeconds = AudioConstants.DefaultPreloadTimeoutSeconds;

        [FormerlySerializedAs("_logThrottledPlays")]
        [Title("Diagnostics")]
        [Tooltip("Log every play refused by the fire-rate gate. Off by default: a throttled footstep "
                 + "filling the console is worse than the throttle it reports.")]
        [SerializeField]
        private bool logThrottledPlays;

        [FormerlySerializedAs("_logDroppedPlays")]
        [Tooltip("Log every play refused because the pool was full. Turn this on if sounds go "
                 + "missing, since there is no voice stealing to cover it up.")]
        [SerializeField]
        private bool logDroppedPlays;

        [FormerlySerializedAs("_generatedIdFolder")]
        [Title("Editor")]
        [Tooltip("Where the generated AudioId class is written. Must sit in an assembly your game "
                 + "code can see.")]
        [FolderPath(ParentFolder = "Assets")]
        [SerializeField]
        private string generatedIdFolder = "DracoRuan/PrebuildServices/AudioSystem/Generated";

        [FormerlySerializedAs("_defaultEntryFolder")]
        [Tooltip("Default folder the Audio Manager offers when saving a new entry.")]
        [FolderPath(ParentFolder = "Assets")]
        [SerializeField]
        private string defaultEntryFolder;

        public AudioMixer Mixer => this.mixer;
        public IReadOnlyList<AudioChannelDefinition> Channels => this.channels;
        public string MasterChannelId => this.masterChannelId;
        public int MaxTotalVoices => this.maxTotalVoices;
        public bool UseUnscaledTime => this.useUnscaledTime;
        public float MaxTickDeltaSeconds => this.maxTickDeltaSeconds;
        public float ClipUnloadGraceSeconds => this.clipUnloadGraceSeconds;
        public float PreloadTimeoutSeconds => this.preloadTimeoutSeconds;
        public bool LogThrottledPlays => this.logThrottledPlays;
        public bool LogDroppedPlays => this.logDroppedPlays;
        public string GeneratedIdFolder => this.generatedIdFolder;
        public string DefaultEntryFolder => this.defaultEntryFolder;
    }
}