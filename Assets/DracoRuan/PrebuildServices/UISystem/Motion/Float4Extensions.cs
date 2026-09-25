using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// Bridges UnityEngine.Vector4 and the engine-agnostic Float4 used by
    /// Motion.Logic's value-mode resolver. Lives here, not on Float4 itself, so
    /// Motion.Logic stays free of a UnityEngine dependency.
    /// </summary>
    internal static class Float4Extensions
    {
        public static Float4 ToFloat4(this Vector4 v) => new Float4(v.x, v.y, v.z, v.w);

        public static Vector4 ToVector4(this Float4 f) => new Vector4(f.X, f.Y, f.Z, f.W);
    }
}
