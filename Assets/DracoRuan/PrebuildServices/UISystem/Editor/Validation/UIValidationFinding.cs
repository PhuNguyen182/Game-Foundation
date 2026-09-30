using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor.DracoRuan.PrebuildServices.UISystem.Editor.Validation
{
    /// <summary>One issue found by UIRegistryValidator, ready to render in an Editor window or
    /// fail a build.</summary>
    public readonly struct UIValidationFinding
    {
        public readonly UIValidationSeverity Severity;
        public readonly string Message;

        /// <summary>The asset this finding is about, if any - lets a window ping/select it.</summary>
        public readonly Object Context;

        public UIValidationFinding(UIValidationSeverity severity, string message, Object context = null)
        {
            this.Severity = severity;
            this.Message = message;
            this.Context = context;
        }

        public override string ToString() => $"[{this.Severity}] {this.Message}";
    }
}
