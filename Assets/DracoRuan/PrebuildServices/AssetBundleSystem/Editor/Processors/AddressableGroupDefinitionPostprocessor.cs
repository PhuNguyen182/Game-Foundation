#if USE_EXTENDED_ADDRESSABLE
using System.Collections.Generic;
using System.IO;
using DracoRuan.PrebuildServices.AssetBundleSystem.Editor.AddressableGroupDefinition;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AssetBundleSystem.Editor.Processors
{
    public class AddressableGroupDefinitionPostprocessor : AssetPostprocessor
    {
        // Triggered after assets are imported/created
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!settings)
                return;

            bool anyChanged = false;

            foreach (string assetPath in importedAssets)
            {
                AddressableGroupDefinitionFile addressableGroupDefinitionFile =
                    AssetDatabase.LoadAssetAtPath<AddressableGroupDefinitionFile>(assetPath);
                if (!addressableGroupDefinitionFile)
                    continue;

                if (string.IsNullOrEmpty(assetPath))
                    continue;
                
                string desiredGroupName = !string.IsNullOrEmpty(addressableGroupDefinitionFile.GroupName)
                    ? addressableGroupDefinitionFile.GroupName
                    : Path.GetFileNameWithoutExtension(assetPath);

                AddressableAssetGroup group = settings.FindGroup(desiredGroupName);
                if (!group)
                {
                    if (!addressableGroupDefinitionFile.template)
                        return;

                    AddressableAssetGroupTemplate groupTemplate = addressableGroupDefinitionFile.BuildAndLoadMode switch
                    {
                        BuildAndLoadMode.Remote => addressableGroupDefinitionFile.template.remoteTemplateGroup,
                        BuildAndLoadMode.Local => addressableGroupDefinitionFile.template.localTemplateGroup,
                        _ => null
                    };
                    
                    if (!groupTemplate)
                        return;
                    
                    List<AddressableAssetGroupSchema> schemasCopy = new();
                    foreach (AddressableAssetGroupSchema schema in groupTemplate.SchemaObjects)
                    {
                        AddressableAssetGroupSchema schemaCopy = Object.Instantiate(schema);
                        schemaCopy.name = schema.name;
                        schemasCopy.Add(schemaCopy);
                    }

                    group = settings.CreateGroup(desiredGroupName, false, false, false, schemasCopy, typeof(BundledAssetGroupSchema));
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, group, true);
                    anyChanged = true;
                }

                // Persist the group name on the asset
                if (addressableGroupDefinitionFile.GroupName == group.Name) 
                    continue;
                
                addressableGroupDefinitionFile.groupName = group.Name;
                EditorUtility.SetDirty(addressableGroupDefinitionFile);
                anyChanged = true;
            }

            if (anyChanged)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
