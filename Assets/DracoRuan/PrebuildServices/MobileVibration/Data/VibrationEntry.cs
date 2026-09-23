using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.Serialization;

namespace DracoRuan.PrebuildServices.MobileVibration.Data
{
    /// <summary>
    /// One authored haptic: what to play, and how often it may repeat.
    /// </summary>
    /// <remarks>
    /// <para>Plain Unity serialization, so the <c>.asset</c> stays readable YAML that merges in
    /// version control. No attributes here drive drawing: the Vibration Manager window and
    /// <c>VibrationEntryEditor</c> both render every field explicitly through
    /// <c>Editor/Drawing/VibrationEntryDrawer</c>.</para>
    ///
    /// <para><b>Nothing here is mutated at runtime.</b> The cooldown timestamp and the currently
    /// playing id live in the service, because state written onto a ScriptableObject survives
    /// leaving play mode in the Editor and would make the second play session of the day behave
    /// differently from the first.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "VibrationEntry", menuName = "DracoRuan/MobileVibration/VibrationEntry")]
    public class VibrationEntry : ScriptableObject
    {
        [FormerlySerializedAs("_id")]
        [Tooltip("Unique name. Generated into the VibrationId class, so it is also a C# member name.")]
        [SerializeField]
        private string id;

        [FormerlySerializedAs("_sourceMode")]
        [Tooltip("Preset fires and returns immediately. Custom Pattern and Curve run over time and "
                 + "keep IsPlaying true while they do.")]
        [SerializeField]
        private VibrationSourceMode sourceMode = VibrationSourceMode.Preset;

        [FormerlySerializedAs("_presetType")] [SerializeField]
        private MOST_HapticFeedback.HapticTypes presetType = MOST_HapticFeedback.HapticTypes.MediumImpact;

        [FormerlySerializedAs("_customPattern")] [SerializeField]
        private MOST_HapticFeedback.CustomHapticPattern customPattern;

        [FormerlySerializedAs("_curve")] [SerializeField] private MOST_HapticFeedback.HapticCurve curve;

        [FormerlySerializedAs("_minIntervalSeconds")]
        [Tooltip("Shortest gap between two plays of this entry. 0 disables the check.")]
        [SerializeField]
        private float minIntervalSeconds;

        public string Id => this.id;
        public VibrationSourceMode SourceMode => this.sourceMode;
        public MOST_HapticFeedback.HapticTypes PresetType => this.presetType;
        public MOST_HapticFeedback.CustomHapticPattern CustomPattern => this.customPattern;
        public MOST_HapticFeedback.HapticCurve Curve => this.curve;

        /// <summary>Shortest gap between two plays of this entry. 0 disables the check.</summary>
        public float MinIntervalSeconds => this.minIntervalSeconds;

        /// <summary>
        /// Whether this entry can produce a haptic at all, and if not, why. Used by the editor tool.
        /// </summary>
        /// <remarks>
        /// A Preset is always playable: <c>MOST_HapticFeedback.HapticTypes</c> is an enum, so there is
        /// nothing to leave unauthored. Custom Pattern and Curve are struct data the author fills in,
        /// and an empty one plays nothing on every platform, so it is worth flagging here rather than
        /// discovering it silently on a device.
        /// </remarks>
        public bool IsPlayable(out string reason)
        {
            switch (this.sourceMode)
            {
                case VibrationSourceMode.CustomPattern:
                    bool hasIOSPulses = this.customPattern.IOS_HapticPattern is { Length: > 0 };
                    bool hasAndroidPulses = this.customPattern.Android_HapticPattern is { Length: > 0 };

                    if (!hasIOSPulses && !hasAndroidPulses)
                    {
                        reason = $"Vibration entry '{this.id}' is set to Custom Pattern but has no iOS "
                                 + "or Android pulses authored.";
                        return false;
                    }

                    reason = null;
                    return true;

                case VibrationSourceMode.Curve:
                    bool hasIntensity = this.curve.IOS_HapticCurve.Intensity is { length: > 0 };

                    if (!hasIntensity)
                    {
                        reason = $"Vibration entry '{this.id}' is set to Curve but has no intensity "
                                 + "curve authored.";
                        return false;
                    }

                    reason = null;
                    return true;

                default:
                    reason = null;
                    return true;
            }
        }
    }
}