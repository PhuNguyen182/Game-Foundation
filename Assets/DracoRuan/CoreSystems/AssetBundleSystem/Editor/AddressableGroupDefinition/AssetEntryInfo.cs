#if USE_EXTENDED_ADDRESSABLE
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Editor.AddressableGroupDefinition
{
    [Serializable]
    public class AssetEntryInfo
    {
        [ReadOnly] [SerializeField] public string address;
        [ReadOnly] [SerializeField] public string assetPath;
        [SerializeField] public List<AssetLabelReference> labels;

        [Button]
        private void Locate()
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }
    }
}
#endif
