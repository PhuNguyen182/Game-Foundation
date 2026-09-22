using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Interfaces
{
    /// <summary>
    /// Owns every audio clip load and release.
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing else may load an entry's clip.</b> An <c>AssetReferenceT</c> keeps its own
    /// internal operation handle, and that instance lives on a shared <see cref="AudioEntry"/>
    /// asset — so it can only hold one load at a time, and two call sites releasing it out of step
    /// corrupt it. Routing every load through here, keyed by entry index and released through the
    /// handle this library stored, is what makes that impossible.</para>
    /// </remarks>
    public interface IAudioClipLibrary : IDisposable
    {
        /// <summary>
        /// The clip for an entry if it is already available without waiting. True for every
        /// direct-reference entry, and for an Addressables entry whose clip is resident.
        /// </summary>
        public bool TryGetLoaded(AudioEntry entry, int entryIndex, out AudioClip clip);

        /// <summary>
        /// The clip for an entry, loading it if necessary. Concurrent callers share one load.
        /// </summary>
        public UniTask<AudioClip> AcquireAsync(
            AudioEntry entry, int entryIndex, CancellationToken cancellation = default);

        /// <summary>
        /// Gives up one reference. The clip is not unloaded straight away: a grace period stops a
        /// footstep played twice a second from thrashing the loader.
        /// </summary>
        public void Release(int entryIndex);

        /// <summary>Loads and pins a clip so it is never unloaded. For entries marked preload.</summary>
        public UniTask PreloadAsync(AudioEntry entry, int entryIndex, CancellationToken cancellation = default);

        /// <summary>Unloads whatever has been idle past its grace period.</summary>
        public void TickSweep(float now);
    }
}
