using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CSVlist", menuName = "Scriptable Objects/CSVlist")]
public class CSVlist : ScriptableObject
{

    [SerializeField] private List<string> csvFiles = new List<string>();

    public string this[int index]
    {
        get { return csvFiles[index]; } 
    }

}
