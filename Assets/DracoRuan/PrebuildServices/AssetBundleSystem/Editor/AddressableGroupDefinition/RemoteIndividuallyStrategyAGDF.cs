#if USE_EXTENDED_ADDRESSABLE
using UnityEngine;

namespace DracoRuan.PrebuildServices.AssetBundleSystem.Editor.AddressableGroupDefinition
{
    [CreateAssetMenu(fileName = "RemoteIndividuallyStrategyAGDF",
        menuName = "ExtendedAddressable/Group Definition/Remote AGDF/RemoteIndividuallyStrategyAGDF")]
    public class RemoteIndividuallyStrategyAGDF : AddressableGroupDefinitionFile
    {
        public override BuildAndLoadMode BuildAndLoadMode => BuildAndLoadMode.Remote;
    }

}
#endif
