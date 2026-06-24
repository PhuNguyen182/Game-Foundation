#if USE_EXTENDED_ADDRESSABLE
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.AddressableAssets;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Editor.AddressableGroupDefinition
{
    [CustomEditor(typeof(LocalChunkedStrategyAGDF))]
    public class LocalChunkedStrategyAGDFEditor : OdinEditor
    {
        private string _folderPath;
        private LocalChunkedStrategyAGDF _target;

        private void Awake()
        {
            _target = (LocalChunkedStrategyAGDF)target;
            ConvertCurrentFolderIntoAddressableGroup();
        }

        private void ConvertCurrentFolderIntoAddressableGroup()
        {
            string soAssetPath = AssetDatabase.GetAssetPath(_target);
            if (string.IsNullOrEmpty(soAssetPath))
                return;

            _folderPath = Path.GetDirectoryName(soAssetPath);
            _target.folderName = _folderPath;
            
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            _target.SetupTemplate();
            string groupName = string.IsNullOrEmpty(_target.GroupName)
                ? Path.GetFileNameWithoutExtension(soAssetPath)
                : _target.GroupName;
            AddressableAssetGroup group = settings.FindGroup(groupName);

            if (group == null)
            {
                List<AddressableAssetGroupSchema> schemasCopy = new();
                foreach (AddressableAssetGroupSchema schema in _target.template.localTemplateGroup.SchemaObjects)
                {
                    AddressableAssetGroupSchema schemaCopy = Instantiate(schema);
                    schemaCopy.name = schema.name;
                    schemasCopy.Add(schemaCopy);
                }
                
                group = settings.CreateGroup(groupName, false, false, false, schemasCopy, typeof(BundledAssetGroupSchema));
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, null, true);
            }

            else if (string.CompareOrdinal(group.Name, groupName) != 0)
            {
                group.Name = groupName;
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRenamed, group, true);
            }

            _target.groupName = group.Name;
            _target.cachedGroupName = group.Name;

            if (string.IsNullOrEmpty(_folderPath) || !AssetDatabase.IsValidFolder(_folderPath))
                return;

            string folderGuid = AssetDatabase.AssetPathToGUID(_folderPath);
            AddressableAssetEntry folderEntry = settings.CreateOrMoveEntry(folderGuid, group);

            // Set address to folder name for clarity
            string folderName = Path.GetFileName(_folderPath);
            if (!string.IsNullOrEmpty(folderName))
                folderEntry.SetAddress(folderName);

            _target.assetEntries.Clear();
            foreach (AddressableAssetEntry entry in group.entries)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                AssetEntryInfo info = new()
                {
                    assetPath = assetPath,
                    address = entry.address,
                    labels = new()
                };

                foreach (string label in entry.labels)
                    info.labels.Add(new AssetLabelReference { labelString = label });

                _target.assetEntries.Add(info);
            }
        }
    }
}
#endif