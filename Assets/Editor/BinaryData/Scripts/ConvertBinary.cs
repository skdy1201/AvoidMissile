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
    private enum CSVType
    {
        Item,
        FallingMissile,
        HoverMissile,

    }

    #region Readcsv
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
                        else if (string.IsNullOrEmpty(now.Percent))
                            now.Percent = cur;
                        else if (string.IsNullOrEmpty(now.Value))
                            now.Value = cur;

                        cur = "";
                    }

                }

                now.Time = cur;

                Debug.Log($"cur ItemData's name is {now.Name} , percent is {now.Percent}, value is {now.Value}, time is {now.Time}");

            }
        }
        else
        {
            Debug.Log("csv fail");

        }


    }
    #endregion Readcsv

    #region ChangeBinary
    /// <summary>
    /// 바이너리 파일 변환 함수
    /// </summary>
    /// <remarks>
    /// ItemSetting.bytes 파일을 만듬
    /// </remarks>
    [MenuItem("Custom/WriteBinary")]
    private static void ChangeBinary()
    {

        CSVlist file = (CSVlist)AssetDatabase.LoadAssetAtPath("Assets/Editor/BinaryData/CSVlist.asset", typeof(CSVlist));

        int csvCount  = System.Enum.GetValues(typeof(CSVType)).Length;

        for(int curcsv = 0; curcsv < csvCount; ++curcsv)
        {
            string fileName = file[curcsv];

            // Unity Project's Asset Folder
            string csvPath = UnityEngine.Application.dataPath + "/Resources/" + fileName;

            switch (curcsv)
            {
                case 0:
                    BinaryItem(csvPath);
                    break;
                case 1:
                    BinaryFallingMissile(csvPath);
                    break;
                case 2:
                    BinaryHoverMissile(csvPath);
                    break;
            }


        }

        
    }
    #endregion ChangeBinary

    #region ReadBinary
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
                        now.Percent = reader.ReadString();
                        now.Value = reader.ReadString();
                        now.Time = reader.ReadString();

                        Debug.Log($"item name is {now.Name}, Percent is {now.Percent}, Value is {now.Value}, Time is {now.Time}");
                    }

                }
            }
        }
    }
    #endregion


    #region ConvertMethod

    /// <summary>
    /// 아이템 바이너리 데이터를 만드는 함수
    /// </summary>
    /// <param name="csvPath"> 아이템 설정 csv 파일 경로 </param>
    private static void BinaryItem(string csvPath)
    {

        List<ItemData> itemDatas = new List<ItemData>();

        if (File.Exists(csvPath))
        {
            // change string array in file's
            string[] datas = File.ReadAllLines(csvPath);

            char spliter = ',';

            for (int lineIndex = 1; lineIndex < datas.Length; lineIndex++)
            {
                ItemData now = new ItemData();

                string[] result = datas[lineIndex].Split(spliter);
               
                for(int curItemData = 0; curItemData < result.Length; curItemData++) 
                {
                   if (string.IsNullOrEmpty(now.Name))
                       now.Name = result[curItemData];
                   else if (string.IsNullOrEmpty(now.Percent))
                       now.Percent = result[curItemData];
                    else if (string.IsNullOrEmpty(now.Value))
                       now.Value = result[curItemData];
                    else
                        now.Time = result[curItemData];

                }
                itemDatas.Add(now);
            }
        }
        else
        {
            Debug.Log("csv fail");
            return;
        }

        string binaryFilePath = UnityEngine.Application.dataPath + "/Resources/" + "itemBinary" + ".bytes";

        if (File.Exists(binaryFilePath))
        {
            File.Delete(binaryFilePath);
        }

        using (var stream = File.Open(binaryFilePath, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(itemDatas.Count);

                for (int i = 0; i < itemDatas.Count; ++i)
                {
                    writer.Write(itemDatas[i].Name);
                    writer.Write(itemDatas[i].Percent);
                    writer.Write(itemDatas[i].Value);
                    writer.Write(itemDatas[i].Time);
                }
            }
        }

        Debug.Log("Convert ItemBinary Finish");
        AssetDatabase.Refresh();
    }

    private static void BinaryFallingMissile(string csvPath)
    {
        FallingMissileSetting fallingMissileSetting = new FallingMissileSetting();

        if (File.Exists(csvPath))
        {
            // change string array in file's
            string[] datas = File.ReadAllLines(csvPath);

            char spliter = ',';

            for(int i = 1; i < datas.Length; ++i)
            {
                string[] cur = datas[i].Split(spliter);

                if (cur[0] == "MissileCount")
                {
                    int.TryParse(cur[1], out fallingMissileSetting.missileCount);
                    int.TryParse(cur[2], out fallingMissileSetting.missileIncrement);
                    int.TryParse(cur[3], out fallingMissileSetting.maxCount);
                }
                else if (cur[0] == "FallSpeed")
                {
                    float.TryParse(cur[1], out fallingMissileSetting.fallSpeed);
                    float.TryParse(cur[2], out fallingMissileSetting.fallIncrement);
                    float.TryParse(cur[3], out fallingMissileSetting.fallSpeedMax);
                }
                else if(cur[0] == "Waiting")
                {
                    float.TryParse(cur[1], out fallingMissileSetting.waiting);
                    float.TryParse(cur[2], out fallingMissileSetting.waitIncrement);
                    float.TryParse(cur[3], out fallingMissileSetting.waitingMax);
                }
            }
        }
        else
        {
            Debug.Log("csv fail");
            return;
        }

        string binaryFilePath = UnityEngine.Application.dataPath + "/Resources/" + "fallingMissieData" + ".bytes";


        if (File.Exists(binaryFilePath))
        {
            File.Delete(binaryFilePath);
        }

        using (var stream = File.Open(binaryFilePath, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(fallingMissileSetting.missileCount);
                writer.Write(fallingMissileSetting.missileIncrement);
                writer.Write(fallingMissileSetting.maxCount);

                writer.Write(fallingMissileSetting.fallSpeed);
                writer.Write(fallingMissileSetting.fallIncrement);
                writer.Write(fallingMissileSetting.fallSpeedMax);

                writer.Write(fallingMissileSetting.waiting);
                writer.Write(fallingMissileSetting.fallIncrement);
                writer.Write(fallingMissileSetting.waitingMax);
            }
        }

        Debug.Log("Convert FallingMissileBinary Finish");
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 추적 미사일 바이너리 데이터를 만드는 함수
    /// </summary>
    /// <param name="csvPath"> 추적 미사일 설정 csv 파일 경로 </param>
    private static void BinaryHoverMissile(string csvPath)
    {
        HoverMissileSetting hoverMissileSetting = new HoverMissileSetting();

        if (File.Exists(csvPath))
        {
            string[] datas = File.ReadAllLines(csvPath);

            char spliter = ',';

            for (int i = 1; i < datas.Length; ++i)
            {
                string[] cur = datas[i].Split(spliter);

                if (cur[0] == "HP")
                {
                    int.TryParse(cur[1], out hoverMissileSetting.hp);
                    int.TryParse(cur[2], out hoverMissileSetting.hpIncrement);
                    int.TryParse(cur[3], out hoverMissileSetting.hpMax);
                }
                else if (cur[0] == "Flight")
                {
                    float.TryParse(cur[1], out hoverMissileSetting.flight);
                    float.TryParse(cur[2], out hoverMissileSetting.flightIncrement);
                    float.TryParse(cur[3], out hoverMissileSetting.flightMax);
                }
                else if (cur[0] == "FlightSpeed")
                {
                    float.TryParse(cur[1], out hoverMissileSetting.flightSpeed);
                    float.TryParse(cur[2], out hoverMissileSetting.flightSpeedIncrement);
                    float.TryParse(cur[3], out hoverMissileSetting.flightSpeedMax);
                }
                else if (cur[0] == "Turn")
                {
                    float.TryParse(cur[1], out hoverMissileSetting.turn);
                    float.TryParse(cur[2], out hoverMissileSetting.turnIncrement);
                    float.TryParse(cur[3], out hoverMissileSetting.turnMax);
                }
                else if (cur[0] == "TurnRate")
                {
                    float.TryParse(cur[1], out hoverMissileSetting.turnRate);
                    float.TryParse(cur[2], out hoverMissileSetting.turnRateIncrement);
                    float.TryParse(cur[3], out hoverMissileSetting.turnRateMax);
                }
            }
        }
        else
        {
            Debug.Log("csv fail");
            return;
        }

        string binaryFilePath = UnityEngine.Application.dataPath + "/Resources/" + "hoverMissileData" + ".bytes";

        if (File.Exists(binaryFilePath))
        {
            File.Delete(binaryFilePath);
        }

        using (var stream = File.Open(binaryFilePath, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(hoverMissileSetting.hp);
                writer.Write(hoverMissileSetting.hpIncrement);
                writer.Write(hoverMissileSetting.hpMax);

                writer.Write(hoverMissileSetting.flight);
                writer.Write(hoverMissileSetting.flightIncrement);
                writer.Write(hoverMissileSetting.flightMax);

                writer.Write(hoverMissileSetting.flightSpeed);
                writer.Write(hoverMissileSetting.flightSpeedIncrement);
                writer.Write(hoverMissileSetting.flightSpeedMax);

                writer.Write(hoverMissileSetting.turn);
                writer.Write(hoverMissileSetting.turnIncrement);
                writer.Write(hoverMissileSetting.turnMax);

                writer.Write(hoverMissileSetting.turnRate);
                writer.Write(hoverMissileSetting.turnRateIncrement);
                writer.Write(hoverMissileSetting.turnRateMax);
            }
        }

        Debug.Log("Convert HoverMissileBinary Finish");
        AssetDatabase.Refresh();
    }

    #endregion
}
