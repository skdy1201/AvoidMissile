using UnityEditor;
using UnityEngine;

public class PlayerPrefDelete : MonoBehaviour
{
    [MenuItem("Window/PlayerPrefs √ ±‚»≠")]
    private static void ResetPrefs()
    {
        PlayerPrefs.DeleteAll();
    }
}
