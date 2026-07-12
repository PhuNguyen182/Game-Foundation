#if USE_EXTENDED_ADDRESSABLE
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Editor.AddressableGroupDefinition
{
    [CreateAssetMenu(fileName = "AddressableGroupDefinitionFileTemplate",
        menuName = "ExtendedAddressable/Group Definition/AddressableGroupDefinitionFileTemplate", order = 1000)]
    public class AddressableGroupDefinitionFileTemplate : ScriptableObject
    {
        [SerializeField] public AddressableAssetGroupTemplate localTemplateGroup;
        [SerializeField] public AddressableAssetGroupTemplate remoteTemplateGroup;
        [SerializeField] public List<string> buildPlatformConstraints;
        [SerializeField] public List<string> buildVersionConstraints;
    }
}
#endif
