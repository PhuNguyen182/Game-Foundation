#if USE_EXTENDED_ADDRESSABLE
using UnityEngine;

namespace DracoRuan.PrebuildServices.AssetBundleSystem.Editor.AddressableGroupDefinition
{
    [CreateAssetMenu(fileName = "LocalChunkedStrategyAGDF",
        menuName = "ExtendedAddressable/Group Definition/Local AGDF/LocalChunkedStrategyAGDF")]
    public class LocalChunkedStrategyAGDF : AddressableGroupDefinitionFile
    {
        public override BuildAndLoadMode BuildAndLoadMode => BuildAndLoadMode.Local;
    }
}
#endif