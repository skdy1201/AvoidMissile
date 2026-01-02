using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 에디터에서 csv 파일을 이용해 게임 상에서 사용할 옵션 세팅을 바이너리파일화
/// </summary>
/// <remarks>
/// csv를 확인, 바이너리 파일화, 바이너리 파일 해석
/// ReadCSV와 ChangeBinary는 같은 코드가 쓰이지만, Editor에서 쓰는 함수인 점,
/// ReadCSVS는 로깅만 하지만, Write는 추가 작업이 있기 때문에, 함수화를 하지 않음
/// </remarks>
public class ConvertBinary : EditorWindow
{
    /// <summary>
    /// csv 파일의 내용을 확인
    /// </summary>
    [MenuItem("Custom/Readcsv")]
    private static void ReadCSV()
    {
        string filename = "ItemSetting.csv";

        // Unity Project's Asset Folder
        string path = Application.dataPath + "/Resources/" + filename;

        char seperator = ',';

        if(File.Exists(path))
        {
            Debug.Log("csv exist");

            // change string array in file's
            string[] datas = File.ReadAllLines(path);

            for (int lineIndex = 1; lineIndex < datas.Length; lineIndex++)
            {
                Debug.Log(datas[lineIndex]);
               
                ItemData now = new ItemData();

                string cur = "";

                for(int i = 0; i < datas[lineIndex].Length; ++i)
                {
                    if (datas[lineIndex][i] != seperator)
                        cur += datas[lineIndex][i];
                    else
                    {
                        Debug.Log(cur);

                        if (string.IsNullOrEmpty(now.Name))
                            now.Name = cur;
                        else if (string.IsNullOrEmpty(now.Type))
                            now.Type = cur;
                        else if (string.IsNullOrEmpty(now.Value))
                            now.Value = cur;

                        cur = "";
                    }

                }

                now.Time = cur;

                Debug.Log($"cur ItemData's name is {now.Name} , type is {now.Type}, value is {now.Value}, time is {now.Time}");

            }
        }
        else
        {
            Debug.Log("csv fail");

        }


    }

    /// <summary>
    /// 바이너리 파일 변환 함수
    /// </summary>
    /// <remarks>
    /// ItemSetting.bytes 파일을 만듬
    /// </remarks>
    [MenuItem("Custom/WriteBinary")]
    private static void ChangeBinary()
    {
        string filename = "ItemSetting.csv";

        // Unity Project's Asset Folder
        string csvPath = UnityEngine.Application.dataPath + "/Resources/" + filename;

        char changechar = ',';

        List<ItemData> itemDatas = new List<ItemData>();

        if (File.Exists(csvPath))
        {
            // change string array in file's
            string[] datas = File.ReadAllLines(csvPath);

            for (int lineIndex = 1; lineIndex < datas.Length; lineIndex++)
            {
                ItemData now = new ItemData();


                string cur = "";

                for (int i = 0; i < datas[lineIndex].Length; ++i)
                {
                    if (datas[lineIndex][i] != changechar)
                        cur += datas[lineIndex][i];
                    else
                    {

                        if (string.IsNullOrEmpty(now.Name))
                            now.Name = cur;
                        else if (string.IsNullOrEmpty(now.Type))
                            now.Type = cur;
                        else if (string.IsNullOrEmpty(now.Value))
                            now.Value = cur;

                        cur = "";
                    }

                }

                now.Time = cur;

                itemDatas.Add(now);
            }
        }
        else
        {
            Debug.Log("csv fail");
            return;
        }

        string binaryFilePath = UnityEngine.Application.dataPath + "/Resources/" + "itemBinary" + ".bytes";

        if(File.Exists(binaryFilePath))
        {
            File.Delete(binaryFilePath);
        }

        using(var stream = File.Open(binaryFilePath, FileMode.Create))
        {
            using(var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(itemDatas.Count);

                for(int i = 0; i <  itemDatas.Count; ++i)
                {
                    writer.Write(itemDatas[i].Name);
                    writer.Write(itemDatas[i].Type);
                    writer.Write(itemDatas[i].Value);
                    writer.Write(itemDatas[i].Time);
                }
            }
        }

        Debug.Log("Convert Binary Finish");
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 만든 바이너리 파일을 확인하는 함수
    /// </summary>
    [MenuItem("Custom/ReadBinary")]
    private static void ReadBinary()
    {
        string binaryFilePath = UnityEngine.Application.dataPath + "/Resources/" + "itemBinary.bytes";

        if(File.Exists(binaryFilePath))
        {
            using (var stream = File.Open(binaryFilePath, FileMode.Open))
            {
                using(var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    int itemCount = reader.ReadInt32();

                    for(int i  = 0; i < itemCount; ++i)
                    {
                        ItemData now = new ItemData();

                        now.Name = reader.ReadString();
                        now.Type = reader.ReadString();
                        now.Value = reader.ReadString();
                        now.Time = reader.ReadString();

                        Debug.Log($"item name is {now.Name}, Type is {now.Type}, Value is {now.Value}, Time is {now.Time}");
                    }

                }
            }
        }
    }
}
