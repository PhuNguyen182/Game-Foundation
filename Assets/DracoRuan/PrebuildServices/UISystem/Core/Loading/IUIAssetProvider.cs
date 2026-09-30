using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core.Loading
{
    /// <summary>Resolves a view definition's prefab (its root UIViewBase). Direct or Addressables, per definition.</summary>
    public interface IUIAssetProvider
    {
        UniTask<UIViewBase> LoadPrefabAsync(UIViewDefinition definition, CancellationToken ct);

        void ReleasePrefab(UIViewDefinition definition, UIViewBase prefab);
    }
}