using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Views;
#if USE_EXTENDED_ADDRESSABLE
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace DracoRuan.PrebuildServices.UISystem.Core.Loading
{
    /// <summary>One definition's loaded prefab and who is still using it. Mirrors
    /// AudioSystem/Core/Loading/AudioClipLease.cs, adapted for UIViewBase prefabs.</summary>
    public sealed class UIPrefabLease
    {
        public UIViewBase Prefab;

        /// <summary>How many open/cached views still need this prefab.</summary>
        public int RefCount;

        /// <summary>Preloaded entries are pinned and never swept.</summary>
        public bool IsPinned;

        /// <summary>Set while a load is in flight, so a burst of opens on a cold prefab issues
        /// a single load rather than one per open.</summary>
        public UniTaskCompletionSource<UIViewBase> Pending;

#if USE_EXTENDED_ADDRESSABLE
        public AsyncOperationHandle<UIViewBase> Handle;
        public bool HasHandle;
#endif
    }
}