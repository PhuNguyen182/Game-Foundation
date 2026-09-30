using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// Adapter assemblies (UISystem.URP) register their IUICameraStacker here at startup; the
    /// core picks the first supported one, then the Built-in stacker when no render pipeline is
    /// set. Returns null on an unsupported pipeline (e.g. HDRP has no camera stacking), which
    /// makes UIService fall back to Screen Space Overlay.
    /// </summary>
    public static class UICameraStackerProvider
    {
        private static readonly List<IUICameraStacker> Registered = new();
        private static readonly BuiltInCameraStacker BuiltIn = new();

        public static void Register(IUICameraStacker stacker)
        {
            if (stacker != null && !Registered.Contains(stacker))
                Registered.Add(stacker);
        }

        public static void Unregister(IUICameraStacker stacker) => Registered.Remove(stacker);

        public static IUICameraStacker Resolve()
        {
            foreach (IUICameraStacker stacker in Registered)
            {
                if (stacker.IsSupported)
                    return stacker;
            }

            return BuiltIn.IsSupported ? BuiltIn : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => Registered.Clear();
    }
}
