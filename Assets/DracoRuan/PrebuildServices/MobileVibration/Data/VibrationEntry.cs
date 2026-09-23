using Sirenix.OdinInspector;
using Solo.MOST_IN_ONE;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Data
{
    /// <summary>
    /// One authored haptic: what to play, and how often it may repeat.
    /// </summary>
    /// <remarks>
    /// <para>Plain Unity serialization, not Odin serialization, so the <c>.asset</c> stays readable
    /// YAML that merges in version control. Odin is used only for inspector attributes, the same
    /// convention <c>AudioEntry</c> follows.</para>
    ///
    /// <para><b>Nothing here is mutated at runtime.</b> The cooldown timestamp and the currently
    /// playing id live in the service, because state written onto a ScriptableObject survives
    /// leaving play mode in the Editor and would make the second play session of the day behave
    /// differently from the first.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "VibrationEntry", menuName = "DracoRuan/MobileVibration/VibrationEntry")]
    public class VibrationEntry : ScriptableObject
    {
        [Title("Identity")]
        [Tooltip("Unique name. Generated into the VibrationId class, so it is also a C# member name.")]
        [SerializeField]
        private string _id;

        [Title("Source")]
        [Tooltip("Preset fires and returns immediately. Custom Pattern and Curve run over time and "
                 + "keep IsPlaying true while they do.")]
        [SerializeField]
        private VibrationSourceMode _sourceMode = VibrationSourceMode.Preset;

        [ShowIf(nameof(_sourceMode), VibrationSourceMode.Preset)] [SerializeField]
        private MOST_HapticFeedback.HapticTypes _presetType = MOST_HapticFeedback.HapticTypes.MediumImpact;

        [ShowIf(nameof(_sourceMode), VibrationSourceMode.CustomPattern)] [SerializeField]
        private MOST_HapticFeedback.CustomHapticPattern _customPattern;

        // Drawn by VibrationManagerWindow itself through a plain SerializedProperty, not by Odin's
        // PropertyTree: HapticCurve nests two more structs (IOS_HapticCurve/Android_HapticCurve),
        // and Odin's fade-group animation for a [ShowIf] field whose content is itself further
        // struct foldouts could not be made to lay out correctly in a manually-driven PropertyTree.
        // Unity's own IMGUI foldout for a plain [Serializable] struct has no such animation and no
        // such bug, so this field steps outside Odin entirely (both its [ShowIf] and its drawing)
        // rather than chase the underlying Odin defect further. HideInInspector only affects
        // PropertyTree/Editor drawing, not serialization, so the field still saves and loads
        // normally; VibrationManagerWindow re-implements the Source Mode == Curve visibility check
        // itself when it draws this field.
        [HideInInspector] [SerializeField] private MOST_HapticFeedback.HapticCurve _curve;

        [Title("Throttling")]
        [Tooltip("Shortest gap between two plays of this entry. 0 disables the check.")]
        [MinValue(0f)]
        [SerializeField]
        private float _minIntervalSeconds;

        public string Id => this._id;
        public VibrationSourceMode SourceMode => this._sourceMode;
        public MOST_HapticFeedback.HapticTypes PresetType => this._presetType;
        public MOST_HapticFeedback.CustomHapticPattern CustomPattern => this._customPattern;
        public MOST_HapticFeedback.HapticCurve Curve => this._curve;

        /// <summary>Shortest gap between two plays of this entry. 0 disables the check.</summary>
        public float MinIntervalSeconds => this._minIntervalSeconds;

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
            switch (this._sourceMode)
            {
                case VibrationSourceMode.CustomPattern:
                    bool hasIOSPulses = this._customPattern.IOS_HapticPattern != null
                                        && this._customPattern.IOS_HapticPattern.Length > 0;
                    bool hasAndroidPulses = this._customPattern.Android_HapticPattern != null
                                            && this._customPattern.Android_HapticPattern.Length > 0;

                    if (!hasIOSPulses && !hasAndroidPulses)
                    {
                        reason = $"Vibration entry '{this._id}' is set to Custom Pattern but has no iOS "
                                 + "or Android pulses authored.";
                        return false;
                    }

                    reason = null;
                    return true;

                case VibrationSourceMode.Curve:
                    bool hasIntensity = this._curve.IOS_HapticCurve.Intensity != null
                                        && this._curve.IOS_HapticCurve.Intensity.length > 0;

                    if (!hasIntensity)
                    {
                        reason = $"Vibration entry '{this._id}' is set to Curve but has no intensity "
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