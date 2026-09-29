using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;
#if USE_EXTENDED_ADDRESSABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace DracoRuan.PrebuildServices.UISystem.Core.Loading
{
    /// <summary>
    /// Addressables-backed IUIAssetProvider (REWRITE_PLAN.md 2.5/6): lease + refcount per
    /// UIViewDefinition, gated behind USE_EXTENDED_ADDRESSABLE, mirroring
    /// AudioSystem/Core/Loading/AudioClipLibrary.cs. A definition with only `prefab` set (no
    /// `addressablePrefab`) behaves exactly like DirectUIAssetProvider for that entry - the two
    /// loading paths coexist per-definition, not per-provider.
    /// </summary>
    public sealed class AddressableUIAssetProvider : IUIAssetProvider, IDisposable
    {
        private readonly Dictionary<UIViewDefinition, UIPrefabLease> _leases = new Dictionary<UIViewDefinition, UIPrefabLease>();
        private bool _isDisposed;

        public UniTask<UIViewBase> LoadPrefabAsync(UIViewDefinition definition, CancellationToken ct)
        {
#if USE_EXTENDED_ADDRESSABLE
            if (definition.AddressablePrefab == null || !definition.AddressablePrefab.RuntimeKeyIsValid())
                return UniTask.FromResult(definition.Prefab);

            return this.AcquireAsync(definition, ct);
#else
            return UniTask.FromResult(definition.Prefab);
#endif
        }

        public void ReleasePrefab(UIViewDefinition definition, UIViewBase prefab)
        {
#if USE_EXTENDED_ADDRESSABLE
            if (definition.AddressablePrefab == null || !definition.AddressablePrefab.RuntimeKeyIsValid())
                return;

            if (!this._leases.TryGetValue(definition, out UIPrefabLease lease))
                return;

            lease.RefCount = lease.RefCount > 0 ? lease.RefCount - 1 : 0;
            if (lease.RefCount <= 0 && !lease.IsPinned)
                this.Unload(definition, lease);
#endif
        }

        /// <summary>Pins the prefab loaded and never released until Dispose or an explicit
        /// unpin - used for `UIViewDefinition.Preload`, same as AudioClipLibrary.PreloadAsync.</summary>
        public async UniTask PreloadAsync(UIViewDefinition definition, CancellationToken ct = default)
        {
#if USE_EXTENDED_ADDRESSABLE
            if (definition.AddressablePrefab == null || !definition.AddressablePrefab.RuntimeKeyIsValid())
                return;

            UIViewBase prefab = await this.AcquireAsync(definition, ct);
            if (prefab == null)
                return;

            if (!this._leases.TryGetValue(definition, out UIPrefabLease lease))
                return;

            lease.IsPinned = true;

            // The pin owns the residency, so the acquire above must not also hold a refcount.
            lease.RefCount = lease.RefCount > 0 ? lease.RefCount - 1 : 0;
#else
            await UniTask.CompletedTask;
#endif
        }

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;

#if USE_EXTENDED_ADDRESSABLE
            var keys = new List<UIViewDefinition>(this._leases.Keys);
            foreach (UIViewDefinition definition in keys)
                this.Unload(definition, this._leases[definition]);
#endif

            this._leases.Clear();
        }

#if USE_EXTENDED_ADDRESSABLE
        private async UniTask<UIViewBase> AcquireAsync(UIViewDefinition definition, CancellationToken ct)
        {
            if (this._leases.TryGetValue(definition, out UIPrefabLease existing))
            {
                if (existing.Prefab != null)
                {
                    existing.RefCount++;
                    return existing.Prefab;
                }

                if (existing.Pending != null)
                {
                    UIViewBase shared = await existing.Pending.Task.AttachExternalCancellation(ct);
                    if (shared != null)
                        existing.RefCount++;

                    return shared;
                }
            }

            UIPrefabLease lease = existing ?? new UIPrefabLease();
            this._leases[definition] = lease;
            lease.Pending = new UniTaskCompletionSource<UIViewBase>();

            UIViewBase loaded;
            try
            {
                AsyncOperationHandle<UIViewBase> handle = definition.AddressablePrefab.LoadAssetAsync<UIViewBase>();
                lease.Handle = handle;
                lease.HasHandle = true;

                loaded = await handle.ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                lease.Pending.TrySetResult(null);
                lease.Pending = null;
                if (lease.RefCount <= 0 && !lease.IsPinned && lease.Prefab == null)
                    this.Unload(definition, lease);
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UISystem] Failed to load Addressable prefab for '{definition.ViewModelType?.Name}': {exception.Message}");
                lease.Pending.TrySetResult(null);
                lease.Pending = null;
                if (lease.RefCount <= 0 && !lease.IsPinned && lease.Prefab == null)
                    this.Unload(definition, lease);
                return null;
            }

            lease.Prefab = loaded;
            lease.RefCount++;
            lease.Pending.TrySetResult(loaded);
            lease.Pending = null;

            return loaded;
        }

        private void Unload(UIViewDefinition definition, UIPrefabLease lease)
        {
            if (lease.HasHandle && lease.Handle.IsValid())
                Addressables.Release(lease.Handle);

            lease.HasHandle = false;
            lease.Prefab = null;
            lease.RefCount = 0;
            lease.IsPinned = false;
            this._leases.Remove(definition);
        }
#endif
    }
}
