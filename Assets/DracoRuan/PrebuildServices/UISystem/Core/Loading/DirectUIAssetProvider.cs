using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Views;

namespace DracoRuan.PrebuildServices.UISystem.Core.Loading
{
    /// <summary>Returns the prefab reference the definition already holds. No loading, no refcounting.</summary>
    public sealed class DirectUIAssetProvider : IUIAssetProvider
    {
        public UniTask<UIViewBase> LoadPrefabAsync(UIViewDefinition definition, CancellationToken ct) =>
            UniTask.FromResult(definition.Prefab);

        public void ReleasePrefab(UIViewDefinition definition, UIViewBase prefab)
        {
            // Direct references are not loaded/leased assets; nothing to release.
        }
    }
}