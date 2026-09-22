using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Interfaces;
using UnityEngine;
#if USE_EXTENDED_ADDRESSABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace DracoRuan.PrebuildServices.AudioSystem.Core.Loading
{
    /// <summary>
    /// The only thing in the system that loads or releases an audio clip.
    /// </summary>
    /// <remarks>
    /// <para>A direct-reference entry never touches the lease machinery at all: its clip is already
    /// there, so <c>Play</c> stays fully synchronous for short sound effects, which is the common
    /// case and the one that must stay cheap.</para>
    ///
    /// <para>For Addressables entries, the important constraint is that an
    /// <c>AssetReferenceT&lt;AudioClip&gt;</c> holds one internal operation handle, and the
    /// instance lives on a shared <see cref="AudioEntry"/> asset. Two voices loading the same entry
    /// through the reference itself, or releasing it out of step, corrupt that handle. Keying
    /// leases by entry index here and releasing through the handle stored in the lease removes the
    /// possibility.</para>
    /// </remarks>
    public sealed class AudioClipLibrary : IAudioClipLibrary
    {
        private const string LogTag = AudioConstants.LogTag;

        private readonly Dictionary<int, AudioClipLease> _leases = new Dictionary<int, AudioClipLease>();
        private readonly List<int> _sweepBuffer = new List<int>();
        private readonly float _unloadGraceSeconds;

        private bool _isDisposed;

        public AudioClipLibrary(float unloadGraceSeconds) => this._unloadGraceSeconds = unloadGraceSeconds;

        public bool TryGetLoaded(AudioEntry entry, int entryIndex, out AudioClip clip)
        {
            clip = null;

            if (entry == null)
                return false;

            if (entry.ClipMode == AudioClipSourceMode.Direct)
            {
                clip = entry.Clip;
                return clip != null || entry.HasVariants;
            }

            if (this._leases.TryGetValue(entryIndex, out AudioClipLease lease) && lease.Clip != null)
            {
                clip = lease.Clip;
                lease.RefCount++;
                lease.ReleaseAtTime = float.PositiveInfinity;
                return true;
            }

            return false;
        }

        public async UniTask<AudioClip> AcquireAsync(
            AudioEntry entry, int entryIndex, CancellationToken cancellation = default)
        {
            if (entry == null)
                return null;

            if (entry.ClipMode == AudioClipSourceMode.Direct)
                return entry.Clip;

#if USE_EXTENDED_ADDRESSABLE
            if (this._leases.TryGetValue(entryIndex, out AudioClipLease existing))
            {
                if (existing.Clip != null)
                {
                    existing.RefCount++;
                    existing.ReleaseAtTime = float.PositiveInfinity;
                    return existing.Clip;
                }

                if (existing.Pending != null)
                {
                    // Share the load already in flight instead of starting a second one.
                    AudioClip shared = await existing.Pending.Task.AttachExternalCancellation(cancellation);
                    if (shared != null)
                        existing.RefCount++;

                    return shared;
                }
            }

            AudioClipLease lease = existing ?? new AudioClipLease();
            this._leases[entryIndex] = lease;
            lease.Pending = new UniTaskCompletionSource<AudioClip>();

            AudioClip loaded = null;

            try
            {
                AsyncOperationHandle<AudioClip> handle = entry.ClipReference.LoadAssetAsync<AudioClip>();
                lease.Handle = handle;
                lease.HasHandle = true;

                loaded = await handle.ToUniTask(cancellationToken: cancellation);
            }
            catch (System.OperationCanceledException)
            {
                lease.Pending.TrySetResult(null);
                lease.Pending = null;
                this.ReleaseLeaseIfUnused(entryIndex, lease);
                throw;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[{LogTag}] Failed to load the clip for audio entry '{entry.Id}': {exception.Message}");
                lease.Pending.TrySetResult(null);
                lease.Pending = null;
                this.ReleaseLeaseIfUnused(entryIndex, lease);
                return null;
            }

            lease.Clip = loaded;
            lease.RefCount++;
            lease.ReleaseAtTime = float.PositiveInfinity;

            lease.Pending.TrySetResult(loaded);
            lease.Pending = null;

            return loaded;
#else
            Debug.LogError($"[{LogTag}] Audio entry '{entry.Id}' is set to Addressables, but "
                           + "USE_EXTENDED_ADDRESSABLE is not defined in this build, so its clip "
                           + "cannot be loaded.");
            await UniTask.CompletedTask;
            return null;
#endif
        }

        public void Release(int entryIndex)
        {
            if (!this._leases.TryGetValue(entryIndex, out AudioClipLease lease))
                return;

            lease.RefCount = lease.RefCount > 0 ? lease.RefCount - 1 : 0;

            if (lease.RefCount > 0 || lease.IsPinned)
                return;

            // Deferred, not immediate: see the grace period note on the lease.
            lease.ReleaseAtTime = this._lastSweepTime + this._unloadGraceSeconds;
        }

        public async UniTask PreloadAsync(AudioEntry entry, int entryIndex, CancellationToken cancellation = default)
        {
            if (entry == null || entry.ClipMode == AudioClipSourceMode.Direct)
                return;

            AudioClip clip = await this.AcquireAsync(entry, entryIndex, cancellation);
            if (clip == null)
                return;

            if (!this._leases.TryGetValue(entryIndex, out AudioClipLease lease))
                return;

            lease.IsPinned = true;
            lease.ReleaseAtTime = float.PositiveInfinity;

            // The pin owns the residency, so the acquire above must not also hold a reference.
            lease.RefCount = lease.RefCount > 0 ? lease.RefCount - 1 : 0;
        }

        public void TickSweep(float now)
        {
            this._lastSweepTime = now;

            if (this._leases.Count == 0)
                return;

            this._sweepBuffer.Clear();

            foreach (KeyValuePair<int, AudioClipLease> pair in this._leases)
            {
                AudioClipLease lease = pair.Value;

                if (lease.IsPinned || lease.RefCount > 0 || lease.Pending != null)
                    continue;

                if (now >= lease.ReleaseAtTime)
                    this._sweepBuffer.Add(pair.Key);
            }

            for (int i = 0; i < this._sweepBuffer.Count; i++)
                this.Unload(this._sweepBuffer[i]);
        }

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;

            this._sweepBuffer.Clear();
            foreach (KeyValuePair<int, AudioClipLease> pair in this._leases)
                this._sweepBuffer.Add(pair.Key);

            for (int i = 0; i < this._sweepBuffer.Count; i++)
                this.Unload(this._sweepBuffer[i]);

            this._leases.Clear();
            this._sweepBuffer.Clear();
        }

        private void ReleaseLeaseIfUnused(int entryIndex, AudioClipLease lease)
        {
            if (lease.RefCount <= 0 && !lease.IsPinned && lease.Clip == null)
                this.Unload(entryIndex);
        }

        private void Unload(int entryIndex)
        {
            if (!this._leases.TryGetValue(entryIndex, out AudioClipLease lease))
                return;

#if USE_EXTENDED_ADDRESSABLE
            if (lease.HasHandle && lease.Handle.IsValid())
                Addressables.Release(lease.Handle);

            lease.HasHandle = false;
#endif

            lease.Clip = null;
            lease.RefCount = 0;
            lease.IsPinned = false;
            this._leases.Remove(entryIndex);
        }

        private float _lastSweepTime;
    }
}
