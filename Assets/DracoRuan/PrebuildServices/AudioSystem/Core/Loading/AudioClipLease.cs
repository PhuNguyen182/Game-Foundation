using Cysharp.Threading.Tasks;
using UnityEngine;
#if USE_EXTENDED_ADDRESSABLE
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace DracoRuan.PrebuildServices.AudioSystem.Core.Loading
{
    /// <summary>One entry's loaded clip and who is still using it.</summary>
    public sealed class AudioClipLease
    {
        public AudioClip Clip;

        /// <summary>How many live voices still need this clip.</summary>
        public int RefCount;

        /// <summary>Preloaded entries are pinned and never swept.</summary>
        public bool IsPinned;

        /// <summary>
        /// Set while a load is in flight. Concurrent requests await this one source, so a burst of
        /// plays on a cold clip issues a single load rather than one per play.
        /// </summary>
        public UniTaskCompletionSource<AudioClip> Pending;

        /// <summary>
        /// When this clip becomes eligible for unloading, on the service clock.
        /// </summary>
        /// <remarks>
        /// The grace period is the point: a footstep played every four hundred milliseconds would
        /// otherwise load and unload its clip continuously.
        /// </remarks>
        public float ReleaseAtTime = float.PositiveInfinity;

#if USE_EXTENDED_ADDRESSABLE
        /// <summary>
        /// The handle this library owns for the load.
        /// </summary>
        /// <remarks>
        /// Releasing through the handle stored here, rather than through the entry's
        /// <c>AssetReference</c>, is what keeps that shared reference's own internal handle
        /// consistent when several voices play the same entry.
        /// </remarks>
        public AsyncOperationHandle<AudioClip> Handle;

        public bool HasHandle;
#endif
    }
}
