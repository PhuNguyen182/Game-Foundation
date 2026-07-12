#if USE_EXTENDED_ADDRESSABLE
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DracoRuan.PrebuildServices.AssetBundleSystem.Editor.AddressableGroupDefinition
{
    // TODO: Fix the bug: if I update remote or local to target group, it crash if I delete AGDF file
    [InfoBox(
        "This is an Addressable Group Definition File. TODO: Every time create new AGDF file, apply AGDF template to it. " +
        "Add green or red color to check this ADGF file is connected to corresponding Addressable Group.")]
    public abstract class AddressableGroupDefinitionFile : ScriptableObject
    {
        public string GroupName => useOverrideName ? overrideName : groupName;
        public abstract BuildAndLoadMode BuildAndLoadMode { get; }
        
        [SerializeField] public AddressableGroupDefinitionFileTemplate template;
        
        [Header("Define Names")]
        [ReadOnly] [SerializeField] [Multiline] public string folderName;
        [ReadOnly] [SerializeField] public string cachedGroupName;

        [ReadOnly] [SerializeField] public string groupName;
        public bool useOverrideName;
        [ShowIf("useOverrideName", true)] public string overrideName;

        [Header("Entries")] [ListDrawerSettings(ShowFoldout = true)] [SerializeField]
        public List<AssetEntryInfo> assetEntries = new();

        [Header("Custom Constrains")]
        [PropertyTooltip("Optional version for custom purposes.")] [SerializeField]
        public List<string> buildVersionConstraints;

        [PropertyTooltip("Optional platform for custom purposes.")] [SerializeField]
        public List<string> buildPlatformConstraints;
        
        public void SetupTemplate()
        {
            if (template != null)
                return;
            
            string[] guids = AssetDatabase.FindAssets("t:AddressableGroupDefinitionFileTemplate");
            if (guids.Length > 0)
            {
                string templatePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                AddressableGroupDefinitionFileTemplate agdfTemplate =
                    AssetDatabase.LoadAssetAtPath<AddressableGroupDefinitionFileTemplate>(templatePath);
                template = agdfTemplate;
                buildPlatformConstraints = template.buildPlatformConstraints;
                buildVersionConstraints = template.buildVersionConstraints;

                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }
        }

        [Button]
        [HorizontalGroup("1")]
        [PropertyTooltip("Update all group entries to match the ChildrenAssets list (addresses, labels, etc.)")]
        private void UpdateGroup()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
                return;

            foreach (AssetEntryInfo child in assetEntries)
            {
                string guid = AssetDatabase.AssetPathToGUID(child.assetPath);
                AddressableAssetEntry entry = settings.FindAssetEntry(guid);

                if (entry == null || entry.parentGroup != group)
                    continue;

                if (!string.IsNullOrEmpty(child.address))
                    entry.SetAddress(child.address);

                entry.labels.Clear();
                if (child.labels != null)
                {
                    foreach (AssetLabelReference labelRef in child.labels)
                    {
                        string label = labelRef.labelString;
                        if (!string.IsNullOrEmpty(label))
                        {
                            if (!settings.GetLabels().Contains(label))
                                settings.AddLabel(label);

                            entry.SetLabel(label, true);
                        }
                    }
                }
            }

            if (useOverrideName)
            {
                group.Name = overrideName;
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRenamed, group, true);
            }
            
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            if (useOverrideName)
            {
                string oldGroupName = cachedGroupName;
                AddressableAssetGroup oldGroup = settings.FindGroup(oldGroupName);
                if (oldGroup != null)
                {
                    settings.RemoveGroup(oldGroup);
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, oldGroup, true);
                }
            }

        }

        [Button]
        [HorizontalGroup("1")]
        [PropertyTooltip("Delete the corresponding Addressable group, then delete this SO asset.")]
        private void DeleteGroup()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            AddressableAssetGroup group = settings.FindGroup(GroupName);
            if (group != null)
            {
                settings.RemoveGroup(group);
                settings.SetDirty(
                    AddressableAssetSettings.ModificationEvent.GroupRemoved,
                    group, true);
            }

            string assetPath = AssetDatabase.GetAssetPath(this);
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.SaveAssets();
        }

        [Button]
        [HorizontalGroup("2")]
        [PropertyTooltip("Set all group entry addresses to their asset name (no extension).")]
        private void SimplifyAddresses()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            AddressableAssetGroup group = settings.FindGroup(GroupName);
            if (group == null)
                return;

            string soAssetPath = AssetDatabase.GetAssetPath(this);
            string soGuid = AssetDatabase.AssetPathToGUID(soAssetPath);
            string folderPath = Path.GetDirectoryName(soAssetPath);
            string[] assetGuids = AssetDatabase.FindAssets("", new[] { folderPath });

            foreach (string guid in assetGuids)
            {
                if (string.CompareOrdinal(guid, soGuid) == 0)
                    continue;

                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(assetPath))
                    continue;

                AddressableAssetEntry entry = settings.FindAssetEntry(guid);
                if (entry != null && entry.parentGroup == group)
                {
                    string simplified = Path.GetFileNameWithoutExtension(assetPath);
                    entry.SetAddress(simplified);
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(this);
        }

        [Button]
        [HorizontalGroup("2")]
        [PropertyTooltip("Ping the corresponding group in the Addressable Groups window.")]
        private void LocateGroup()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            AddressableAssetGroup group = settings.FindGroup(GroupName);
            if (group != null)
            {
                Selection.activeObject = group;
                EditorGUIUtility.PingObject(group);
            }
        }
    }
}
#endif
