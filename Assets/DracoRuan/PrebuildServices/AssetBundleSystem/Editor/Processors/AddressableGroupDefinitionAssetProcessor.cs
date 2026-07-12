#if USE_EXTENDED_ADDRESSABLE
using System.IO;
using DracoRuan.PrebuildServices.AssetBundleSystem.Editor.AddressableGroupDefinition;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AssetBundleSystem.Editor.Processors
{
    public class AddressableGroupDefinitionAssetProcessor : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            AddressableGroupDefinitionFile def =
                AssetDatabase.LoadAssetAtPath<AddressableGroupDefinitionFile>(assetPath);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            if (!settings)
                return AssetDeleteResult.DidNotDelete;

            string groupName;
            if (def && !string.IsNullOrEmpty(def.GroupName))
                groupName = def.GroupName;
            else
                groupName = Path.GetFileNameWithoutExtension(assetPath);

            AddressableAssetGroup group = settings.FindGroup(groupName);
            string folderPath = Path.GetDirectoryName(assetPath);

            string[] assetGuids = AssetDatabase.FindAssets("", new[] { folderPath });
            foreach (string guid in assetGuids)
            {
                AddressableAssetEntry assetEntry = settings.FindAssetEntry(guid);
                if (assetEntry != null && assetEntry.parentGroup == group)
                    settings.RemoveAssetEntry(guid);
            }

            if (group)
            {
                settings.RemoveGroup(group);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, group, true);
                Debug.Log($"[AddressableGroupDefinition] Group '{groupName}' has been removed.");
            }

            AssetDatabase.SaveAssets();
            return AssetDeleteResult.DidNotDelete;
        }
    }
}
#endif
