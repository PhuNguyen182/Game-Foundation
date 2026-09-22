using DracoRuan.PrebuildServices.AudioSystem.Data.Attributes;
using UnityEditor;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor.Drawers
{
    /// <summary>Draws an <c>[AudioChannelId]</c> string field as a dropdown of declared channels.</summary>
    [CustomPropertyDrawer(typeof(AudioChannelIdAttribute))]
    public sealed class AudioChannelIdDrawer : AudioIdDropdownDrawer
    {
        protected override string[] SourceIds => AudioIdIndex.ChannelIds;

        protected override string[] SourceLabels => AudioIdIndex.ChannelLabels;

        protected override bool AllowEmpty => ((AudioChannelIdAttribute)this.attribute).AllowEmpty;

        protected override string MissingHint =>
            "'{0}' is not a channel in the AudioConfig. Add it there, or pick one that exists.";
    }
}
