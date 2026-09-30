#if USE_EXTENDED_ADDRESSABLE
using System;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine.AddressableAssets;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>Non-generic subclass so the Inspector serializes it and restricts the picker to
    /// prefabs whose root carries a UIViewBase. Serializes the same GUID payload as
    /// AssetReferenceGameObject, so existing definitions keep their reference.</summary>
    [Serializable]
    public sealed class AssetReferenceUIView : AssetReferenceT<UIViewBase>
    {
        public AssetReferenceUIView(string guid) : base(guid)
        {
        }
    }
}
#endif
