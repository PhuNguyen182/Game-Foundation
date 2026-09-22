using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>One reason a set of audio ids cannot be generated as written.</summary>
    public readonly struct AudioIdConflict
    {
        public AudioIdConflictKind Kind { get; }

        /// <summary>A message that names every owner involved, ready to show verbatim.</summary>
        public string Message { get; }

        public IReadOnlyList<string> OwnerPaths { get; }

        public AudioIdConflict(AudioIdConflictKind kind, string message, IReadOnlyList<string> ownerPaths)
        {
            this.Kind = kind;
            this.Message = message;
            this.OwnerPaths = ownerPaths;
        }
    }
}
