using Object = UnityEngine.Object;

namespace DracoRuan.Foundation.DataFlow.StaticData
{
    /// <summary>
    /// Destroys a runtime ScriptableObject from either play mode or the Editor.
    /// </summary>
    /// <remarks>
    /// <c>Object.Destroy</c> throws outside play mode, which would make every EditMode test that
    /// exercises the JSON path fail on cleanup rather than on the thing it is testing — and would
    /// make an Editor tool that previews a config leak an instance per preview.
    /// </remarks>
    internal static class StaticDataObjectLifetime
    {
        public static void Destroy(Object target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                Object.DestroyImmediate(target);
                return;
            }
#endif
            Object.Destroy(target);
        }
    }
}
