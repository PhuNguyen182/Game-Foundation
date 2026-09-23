using DracoRuan.PrebuildServices.MobileVibration.Data.Attributes;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Drawers
{
    /// <summary>Draws a <c>[VibrationId]</c> string field as a dropdown of the project's vibration ids.</summary>
    [CustomPropertyDrawer(typeof(VibrationIdAttribute))]
    public sealed class VibrationIdDrawer : VibrationIdDropdownDrawer
    {
        protected override string[] SourceIds => VibrationIdIndex.EntryIds;

        protected override string[] SourceLabels => VibrationIdIndex.EntryLabels;

        protected override bool AllowEmpty => ((VibrationIdAttribute)this.attribute).AllowEmpty;

        protected override string MissingHint =>
            "'{0}' does not match any VibrationEntry in the project. It was probably renamed or "
            + "deleted. Pick a replacement; the value is left as it is until you do.";
    }
}
