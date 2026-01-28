using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CSVlist", menuName = "Scriptable Objects/CSVlist")]
public class CSVlist : ScriptableObject
{
    [SerializeField] List<string> csvFiles = new List<string>();

}
