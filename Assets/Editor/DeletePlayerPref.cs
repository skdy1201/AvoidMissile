using UnityEditor;
using UnityEngine;

public class PlayerPrefDelete : EditorWindow
{
    [MenuItem("Custom/PlayerPrefs √ ±‚»≠")]
    private static void ResetPrefs()
    {
        PlayerPrefs.DeleteAll();
    }
}
