using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor;
using System.IO;

public class BuildPostProcessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string targetPath = "Assets/Resources/PlaceholderAds/Rewarded/768x1024.prefab";

        // Remove the copied prefab after the build is done
        if (File.Exists(targetPath))
        {
            AssetDatabase.DeleteAsset(targetPath);
        }

        // Additional build post-processing steps
    }
}