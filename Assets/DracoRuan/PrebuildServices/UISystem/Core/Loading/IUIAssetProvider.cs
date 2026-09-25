using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Data;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Core.Loading
{
    /// <summary>Resolves a view definition's prefab. Direct now; Addressables lands in a later phase.</summary>
    public interface IUIAssetProvider
    {
        UniTask<GameObject> LoadPrefabAsync(UIViewDefinition definition, CancellationToken ct);

        void ReleasePrefab(UIViewDefinition definition, GameObject prefab);
    }
}
