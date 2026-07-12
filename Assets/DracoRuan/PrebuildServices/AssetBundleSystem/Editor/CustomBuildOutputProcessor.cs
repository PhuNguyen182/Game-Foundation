#if USE_EXTENDED_ADDRESSABLE
using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AssetBundleSystem.Editor
{
    public class CustomBuildOutputProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        
        public void OnPostprocessBuild(BuildReport report)
        {
            // This function is only work if build a full player, not build Addressable
            
            string version = Application.version;
            string buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            string sourcePath = Path.Combine("ServerData", buildTarget);
            string outputPath = Path.Combine("ServerData", buildTarget, version);
            string outputFullPath = Path.Combine(Directory.GetCurrentDirectory(), outputPath);

            if (!Directory.Exists(outputFullPath))
            {
                Debug.LogWarning($"Bundle output folder not found: {outputFullPath}");
                Directory.CreateDirectory(outputFullPath);
            }

            MoveFolderContents(sourcePath, outputFullPath);
            Debug.Log($"Moved all bundles into version folder: {outputFullPath}");
        }

        private void MoveFolderContents(string sourceFolder, string targetFolder)
        {
            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);

                // Skip files inside the target folder itself
                if (file.StartsWith(targetFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                string destFile = Path.Combine(targetFolder, fileName);
                File.Move(file, destFile);
            }

            foreach (string folder in Directory.GetDirectories(sourceFolder))
            {
                if (folder.Equals(targetFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                string folderName = Path.GetFileName(folder);
                string destFolder = Path.Combine(targetFolder, folderName);
                Directory.Move(folder, destFolder);
            }
        }

        [MenuItem("Tools/Addressables/Build And Move Bundles")]
        private static void MenuBuildAndMoveBundles()
        {
            AddressableAssetSettings.BuildPlayerContent();
        }
    }
}
#endif