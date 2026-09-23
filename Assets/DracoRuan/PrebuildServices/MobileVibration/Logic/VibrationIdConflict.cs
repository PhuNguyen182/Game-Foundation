using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>One reason a set of vibration ids cannot be generated as written.</summary>
    public readonly struct VibrationIdConflict
    {
        public VibrationIdConflictKind Kind { get; }

        /// <summary>A message that names every owner involved, ready to show verbatim.</summary>
        public string Message { get; }

        public IReadOnlyList<string> OwnerPaths { get; }

        public VibrationIdConflict(VibrationIdConflictKind kind, string message, IReadOnlyList<string> ownerPaths)
        {
            this.Kind = kind;
            this.Message = message;
            this.OwnerPaths = ownerPaths;
        }
    }
}
