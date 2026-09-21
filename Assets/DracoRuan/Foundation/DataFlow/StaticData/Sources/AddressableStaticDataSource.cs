using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>
    /// Reads static data from Addressables.
    /// </summary>
    /// <remarks>
    /// <para><b>Every handle is released, including the failed ones.</b> Addressables ref-counts the
    /// handle, not the result, so a load that failed still holds one until released. The previous
    /// implementation returned early on failure without releasing — and since this sits in the
    /// middle of a fallback chain, failure is the ordinary path, not the rare one. The
    /// <c>finally</c> here releases unless the handle was handed to the caller inside a payload.</para>
    ///
    /// <para>Awaits the handle directly instead of polling <c>IsDone</c> once per frame. The poll
    /// cost a frame on every load even when the asset was already in memory.</para>
    /// </remarks>
    public sealed class AddressableStaticDataSource : IStaticDataSource
    {
        private const string LogTag = "StaticData/Addressable";

        public StaticDataSourceType SourceType => StaticDataSourceType.Addressable;

        public async UniTask<StaticDataPayload> LoadAsync(string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(key))
                return StaticDataPayload.Missing();

            AsyncOperationHandle<Object> handle = default;
            bool handedToCaller = false;

            try
            {
                handle = Addressables.LoadAssetAsync<Object>(key);
                Object asset = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (asset == null)
                    return StaticDataPayload.Missing();

                handedToCaller = true;
                return asset is TextAsset textAsset
                    ? StaticDataPayload.FromText(textAsset.text, handle)
                    : StaticDataPayload.FromAsset(asset, handle);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // An address that is not in the catalog is the normal "this source has nothing"
                // answer for a fallback chain, so this is a warning rather than an error.
                Debug.LogWarning($"[{LogTag}] '{key}' could not be read: {exception.Message}");
                return StaticDataPayload.Missing();
            }
            finally
            {
                if (!handedToCaller && handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        public void Release(in StaticDataPayload payload)
        {
            if (payload.ReleaseHandle is not AsyncOperationHandle<Object> handle || !handle.IsValid())
                return;

            try
            {
                Addressables.Release(handle);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{LogTag}] Failed to release handle: {exception.Message}");
            }
        }
    }
}
