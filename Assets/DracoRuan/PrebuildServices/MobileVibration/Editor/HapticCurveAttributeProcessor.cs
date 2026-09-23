using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Solo.MOST_IN_ONE;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>
    /// Makes Odin draw <c>HapticCurve</c>'s two nested structs inline instead of as foldouts.
    /// </summary>
    /// <remarks>
    /// <para><c>MOST_HapticFeedback.HapticCurve</c> (a vendored plugin type, not ours to edit) holds
    /// two plain <c>[Serializable]</c> structs, <c>IOS_HapticCurve</c> and <c>Android_HapticCurve</c>.
    /// Odin's default composite drawer renders a plain nested struct as its own foldout, and
    /// <c>VibrationEntry._curve</c> is itself behind a <c>[ShowIf]</c> fade group - so expanding
    /// either child foldout animates a fade group nested inside another fade group, and expanding
    /// both at once compounds their height computations against each other. In
    /// <c>VibrationManagerWindow</c> that showed up as large blank gaps and fields silently failing
    /// to draw past the bad foldout.</para>
    ///
    /// <para><c>AudioEntry</c> never hits this because none of its <c>[ShowIf]</c>-gated fields
    /// themselves contain further struct foldouts. Inlining the two child structs here removes the
    /// nested fade groups entirely, which fixes the cause rather than the window's fade-group
    /// duration (see <c>VibrationManagerWindow.NoFoldoutAnimationScope</c>, which only covers the
    /// first-draw NaN-height case, not this one).</para>
    /// </remarks>
    public sealed class HapticCurveAttributeProcessor : OdinAttributeProcessor<MOST_HapticFeedback.HapticCurve>
    {
        public override void ProcessChildMemberAttributes(
            InspectorProperty parentProperty, MemberInfo member, List<Attribute> attributes)
        {
            // TEMPORARILY DISABLED for diagnosis: return immediately so Odin falls back to its
            // default foldout rendering, to isolate whether InlineProperty itself is what makes
            // Delay/DurationMs disappear, or whether those two fields were already broken before
            // InlineProperty was introduced (e.g. Unity's MinAttribute does not support `long`, and
            // AndroidHapticCurve.Delay/DurationMs are long fields carrying [Min]).
            return;
#pragma warning disable CS0162
            if (member.Name != nameof(MOST_HapticFeedback.HapticCurve.IOS_HapticCurve)
                && member.Name != nameof(MOST_HapticFeedback.HapticCurve.Android_HapticCurve))
                return;

            attributes.Add(new InlinePropertyAttribute());
            attributes.Add(new HideLabelAttribute());
            attributes.Add(new TitleAttribute(member.Name.Replace('_', ' ')));
#pragma warning restore CS0162
        }
    }
}