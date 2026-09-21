using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>
    /// Reads static data from a <c>Resources</c> folder.
    /// </summary>
    /// <remarks>
    /// A <c>TextAsset</c> comes back as <see cref="StaticDataPayloadKind.Text"/> and anything else as
    /// <see cref="StaticDataPayloadKind.Asset"/>, so this one source feeds both branches: a CSV table
    /// and a ScriptableObject config each ask for their own key and get the shape their decoder wants.
    /// </remarks>
    public sealed class ResourcesStaticDataSource : IStaticDataSource
    {
        private const string LogTag = "StaticData/Resources";

        public StaticDataSourceType SourceType => StaticDataSourceType.Resources;

        public async UniTask<StaticDataPayload> LoadAsync(string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(key))
                return StaticDataPayload.Missing();

            Object asset;
            try
            {
                // Typed as Object rather than the untyped overload: the untyped one can pick a
                // sub-asset when a path is ambiguous, and then reports a type mismatch that names
                // neither what it found nor what was wanted.
                asset = await Resources.LoadAsync<Object>(key).ToUniTask(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{LogTag}] '{key}' could not be read: {exception.Message}");
                return StaticDataPayload.Missing();
            }

            if (asset == null)
                return StaticDataPayload.Missing();

            // The handle is the asset itself — that is what Resources.UnloadAsset needs back.
            return asset is TextAsset textAsset
                ? StaticDataPayload.FromText(textAsset.text, textAsset)
                : StaticDataPayload.FromAsset(asset, asset);
        }

        public void Release(in StaticDataPayload payload)
        {
            // `is not Object` also covers null; the `== null` that follows is Unity's lifetime check
            // for an already-destroyed asset, which reference equality would miss.
            if (payload.ReleaseHandle is not Object asset || asset == null)
                return;

            try
            {
                Resources.UnloadAsset(asset);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{LogTag}] Failed to unload '{asset.name}': {exception.Message}");
            }
        }
    }
}
