using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;


public class BuildPreProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string sourcePath = "Packages/com.google.ads.mobile/GoogleMobileAds/Editor/Resources/PlaceholderAds/Rewarded/768x1024.prefab";
        string targetPath = "Assets/Resources/PlaceholderAds/Rewarded/768x1024.prefab";

        // Ensure the target directory exists
        string targetDirectory = Path.GetDirectoryName(targetPath);
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        // Copy the prefab
        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
        {
            Debug.LogError($"Failed to copy prefab from {sourcePath} to {targetPath}");
        }

        // Additional build pre-processing steps
    }
}
