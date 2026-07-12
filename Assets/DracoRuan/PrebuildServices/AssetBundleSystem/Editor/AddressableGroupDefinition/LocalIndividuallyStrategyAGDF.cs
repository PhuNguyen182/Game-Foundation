#if USE_EXTENDED_ADDRESSABLE
using UnityEngine;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Editor.AddressableGroupDefinition
{
    [CreateAssetMenu(fileName = "LocalIndividuallyStrategyAGDF",
        menuName = "ExtendedAddressable/Group Definition/Local AGDF/LocalIndividuallyStrategyAGDF")]
    public class LocalIndividuallyStrategyAGDF : AddressableGroupDefinitionFile
    {
        public override BuildAndLoadMode BuildAndLoadMode => BuildAndLoadMode.Local;
    }
}
#endif