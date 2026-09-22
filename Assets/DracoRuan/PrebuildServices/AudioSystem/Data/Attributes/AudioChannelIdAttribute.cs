using System;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Data.Attributes
{
    /// <summary>
    /// Draws a string field as a dropdown of the channels declared in the project's
    /// <see cref="AudioConfig"/>.
    /// </summary>
    /// <remarks>Same mechanism and the same reasoning as <see cref="AudioIdAttribute"/>.</remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class AudioChannelIdAttribute : PropertyAttribute
    {
        public AudioChannelIdAttribute(bool allowEmpty = false) => this.AllowEmpty = allowEmpty;

        /// <summary>Whether an unrouted field is legitimate.</summary>
        public bool AllowEmpty { get; }
    }
}
