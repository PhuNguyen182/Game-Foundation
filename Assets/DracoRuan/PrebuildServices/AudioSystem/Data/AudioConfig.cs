using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// Everything about the audio system that is a property of the game rather than of one sound.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "DracoRuan/AudioSystem/AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [Title("Mixer")] [Required] [SerializeField]
        private AudioMixer _mixer;

        [Tooltip("Every channel this game has. Order does not matter.")]
        [ListDrawerSettings(ShowFoldout = true)]
        [SerializeField]
        private List<AudioChannelDefinition> _channels = new List<AudioChannelDefinition>();

        [Tooltip("Which channel stands for the mixer's master bus. Nothing special-cases it beyond "
                 + "SetMasterVolume routing here.")]
        [SerializeField]
        private string _masterChannelId = AudioConstants.DefaultMasterChannelId;

        [Title("Voices")]
        [Tooltip("Ceiling on simultaneous voices. Mobile hardware manages roughly 24 to 32 before "
                 + "Unity starts virtualising by priority on its own.")]
        [MinValue(1)]
        [SerializeField]
        private int _maxTotalVoices = AudioConstants.DefaultMaxTotalVoices;

        [Title("Timing")]
        [Tooltip("Run fades on unscaled time. AudioSource playback ignores timeScale, so a fade that "
                 + "froze at timeScale 0 would be the odd one out.")]
        [SerializeField]
        private bool _useUnscaledTime = true;

        [Tooltip("Largest delta a single tick will act on. Guards the first frame after the app "
                 + "returns from the background, where delta time can be tens of seconds.")]
        [MinValue(0.01f)]
        [SerializeField]
        private float _maxTickDeltaSeconds = AudioConstants.DefaultMaxTickDeltaSeconds;

        [Title("Loading")]
        [Tooltip("How long an Addressables clip stays resident after its last voice releases it.")]
        [MinValue(0f)]
        [SerializeField]
        private float _clipUnloadGraceSeconds = AudioConstants.DefaultClipUnloadGraceSeconds;

        [Tooltip("Per-entry budget for a preload, so a stall names the clip instead of timing out "
                 + "the whole boot pipeline with nothing to go on.")]
        [MinValue(1f)]
        [SerializeField]
        private float _preloadTimeoutSeconds = AudioConstants.DefaultPreloadTimeoutSeconds;

        [Title("Diagnostics")]
        [Tooltip("Log every play refused by the fire-rate gate. Off by default: a throttled footstep "
                 + "filling the console is worse than the throttle it reports.")]
        [SerializeField]
        private bool _logThrottledPlays;

        [Tooltip("Log every play refused because the pool was full. Turn this on if sounds go "
                 + "missing, since there is no voice stealing to cover it up.")]
        [SerializeField]
        private bool _logDroppedPlays;

        [Title("Editor")]
        [Tooltip("Where the generated AudioId class is written. Must sit in an assembly your game "
                 + "code can see.")]
        [FolderPath(ParentFolder = "Assets")]
        [SerializeField]
        private string _generatedIdFolder = "DracoRuan/PrebuildServices/AudioSystem/Generated";

        [Tooltip("Default folder the Audio Manager offers when saving a new entry.")]
        [FolderPath(ParentFolder = "Assets")]
        [SerializeField]
        private string _defaultEntryFolder;

        public AudioMixer Mixer => this._mixer;
        public IReadOnlyList<AudioChannelDefinition> Channels => this._channels;
        public string MasterChannelId => this._masterChannelId;
        public int MaxTotalVoices => this._maxTotalVoices;
        public bool UseUnscaledTime => this._useUnscaledTime;
        public float MaxTickDeltaSeconds => this._maxTickDeltaSeconds;
        public float ClipUnloadGraceSeconds => this._clipUnloadGraceSeconds;
        public float PreloadTimeoutSeconds => this._preloadTimeoutSeconds;
        public bool LogThrottledPlays => this._logThrottledPlays;
        public bool LogDroppedPlays => this._logDroppedPlays;
        public string GeneratedIdFolder => this._generatedIdFolder;
        public string DefaultEntryFolder => this._defaultEntryFolder;
    }
}