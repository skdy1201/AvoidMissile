using UnityEditor;
using UnityEngine;

public class PlayerPrefDelete : EditorWindow
{
    [MenuItem("Custom/PlayerPrefs 초기화")]
    private static void ResetPrefs()
    {
        PlayerPrefs.DeleteAll();
    }
}
