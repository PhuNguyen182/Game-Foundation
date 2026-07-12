#if USE_EXTENDED_ADDRESSABLE
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.AddressableAssets;

namespace DracoRuan.PrebuildServices.AssetBundleSystem.Editor.AddressableGroupDefinition
{
    [CustomEditor(typeof(LocalIndividuallyStrategyAGDF))]
    public class LocalIndividuallyStrategyAGDFEditor : OdinEditor
    {
        private string _folderPath;
        private LocalIndividuallyStrategyAGDF _target;

        private void Awake()
        {
            _target = (LocalIndividuallyStrategyAGDF)target;
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
            // Assign all assets in the same folder (except self) to the group
            string[] assetGuids = AssetDatabase.FindAssets("", new[] { _folderPath });

            foreach (string guid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(assetPath))
                    continue;

                settings.CreateOrMoveEntry(guid, group);
            }

            // Update def.childrenAssets to reflect all entries in the group (except self)
            _target.assetEntries.Clear();
            foreach (AddressableAssetEntry entry in group.entries)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (AssetDatabase.IsValidFolder(assetPath))
                    continue;

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

            EditorUtility.SetDirty(_target);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif