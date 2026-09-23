using System;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Data.Attributes
{
    /// <summary>
    /// Draws a string field as a dropdown of the vibration ids that exist in the project.
    /// </summary>
    /// <remarks>
    /// <para>This lives in the runtime assembly and the drawer lives in the editor one; Unity joins
    /// them through <c>CustomPropertyDrawer</c>. That is what lets the dropdown reach every
    /// serialized string in the project without the runtime assembly knowing editor code exists.
    /// </para>
    ///
    /// <para>Deliberately a <c>PropertyDrawer</c> rather than Odin's <c>ValueDropdown</c>: the Odin
    /// attribute only takes effect on surfaces Odin draws, so a hand-rolled inspector, a
    /// <c>ReorderableList</c> element or a UI Toolkit inspector would silently fall back to a plain
    /// text box — putting typo'd ids back in exactly the places nobody is looking.</para>
    ///
    /// <para>Put this on fields that <i>refer to</i> a vibration. It does not belong on
    /// <see cref="VibrationEntry"/>'s own id field, which is the field that <i>defines</i> one.</para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class VibrationIdAttribute : PropertyAttribute
    {
        public VibrationIdAttribute(bool allowEmpty = false) => this.AllowEmpty = allowEmpty;

        /// <summary>Whether "no vibration" is a legitimate value for this field.</summary>
        public bool AllowEmpty { get; }
    }
}
