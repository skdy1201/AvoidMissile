using UnityEditor;
using UnityEngine;
using System.IO;
using Mono.Cecil;

struct ItemData
{
    public string Name;
    public string Type;
    public string value;
    public string time;
}

public class ConvertBinary : EditorWindow
{
    //Test 함수 : log 를 통해 csv 데이터가 잘 읽히는 지 확인
    [MenuItem("Custom/ConvertItemData")]
    private static void ReadCSV()
    {
        string filename = "ItemSetting.csv";

        // Unity Project's Asset Folder
        string path = Application.dataPath + "/Resources/" + filename;

        char changechar = ',';

        if(File.Exists(path))
        {
            Debug.Log("csv exist");

            // change string array in file's
            string[] datas = File.ReadAllLines(path);

            ItemData now = new ItemData();

            for (int lineIndex = 1; lineIndex < datas.Length; lineIndex++)
            {
                Debug.Log(datas[lineIndex]);

                string cur = "";

                for(int i = 0; i < datas[lineIndex].Length; ++i)
                {
                    if (datas[lineIndex][i] != changechar)
                        cur += datas[lineIndex][i];
                    else
                    {
                        Debug.Log(cur);

                        if (string.IsNullOrEmpty(now.Name))
                            now.Name = cur;
                        else if (string.IsNullOrEmpty(now.Type))
                            now.Type = cur;
                        else if (string.IsNullOrEmpty(now.value))
                            now.value = cur;

                        cur = "";
                    }

                    now.time = cur;

                }

                Debug.Log($"cur ItemData's name is {now.Name} , type is {now.Type}, value is {now.value}, time is {now.time}");

            }
        }
        else
        {
            Debug.Log("csv fail");

        }


    }

    private static void ChangeBinary()
    {
    }
}
