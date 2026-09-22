using DracoRuan.PrebuildServices.AudioSystem.Data.Attributes;
using UnityEditor;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor.Drawers
{
    /// <summary>Draws an <c>[AudioId]</c> string field as a dropdown of the project's audio ids.</summary>
    [CustomPropertyDrawer(typeof(AudioIdAttribute))]
    public sealed class AudioIdDrawer : AudioIdDropdownDrawer
    {
        protected override string[] SourceIds => AudioIdIndex.EntryIds;

        protected override string[] SourceLabels => AudioIdIndex.EntryLabels;

        protected override bool AllowEmpty => ((AudioIdAttribute)this.attribute).AllowEmpty;

        protected override string MissingHint =>
            "'{0}' does not match any AudioEntry in the project. It was probably renamed or deleted. "
            + "Pick a replacement; the value is left as it is until you do.";
    }
}
