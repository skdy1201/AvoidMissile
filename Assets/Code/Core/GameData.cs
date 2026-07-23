using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
public enum GameDataKey
{
    Rank,
    MissileAlpha,
    MainSound,
    EffectSound,
    ActiveParticle,
}

/// <summary>
/// 게임의 전역적인 데이터를 관리하는 싱글톤
/// </summary>
public class GameData : Singleton<GameData>
{

    #region Serialized Fields

    [SerializeField] List<string> gameDataKeys = new List<string>();

    [SerializeField] List<int> rankScore = new List<int>();

    [SerializeField] float missileAlpha = 1f;
    [SerializeField] float mainSound = 0.5f;
    [SerializeField] float effectSound = 0.5f;

    [SerializeField] Dictionary<string, ItemData> itemDatas = new Dictionary<string, ItemData>();

    #endregion

    #region Private/Protected Fields

    /// <summary>
    /// 씬이 시작할 때, 옵션들을 동기화 해주는 작업을 함
    /// </summary>
    protected override void StartProtocol()
    {
        SyncPlayerPref();
    }

    /// <summary>
    /// 마지막으로 설정된 옵션을 다시 저장함
    /// </summary>
    /// <remarks>
    /// 미사일 투명도, BGM, 효과음에 대한 사항들을 저장
    /// </remarks>
    protected override void EndProtocol()
    {
        SetOption();
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Awake가 실행되면, 랭킹 리스트의 사이즈를 조절하고, 옵션을 동기화
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        GameProgress.StartScene.AddListener(() => StartProtocol());
        GameProgress.EndScene.AddListener(() => EndProtocol());

        for (int i = 0; i < 5; ++i)
        {
            rankScore.Add(0);
        }

        SyncPlayerPref();

        LoadItemData();
        LoadMissileData();
        LoadMissilePatternData();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 점수 저장 함수
    /// </summary>
    /// <param name="score"> 점수 </param>
    /// <remarks>
    /// 기존 랭킹 리스트에 더해두고, 정렬해서 5개만 추출
    /// </remarks>
    public void SaveScore(int score)
    {
        rankScore.Add(score);
        rankScore.Sort((a, b) => b.CompareTo(a));

        // 상위 5개
        if (rankScore.Count > 5)
        {
            rankScore.RemoveAt(rankScore.Count - 1);
        }

        // PlayerPref에 저장
        string key = "Rank";

        for (int i = 0; i < rankScore.Count; ++i)
        {
            PlayerPrefs.SetInt(key + (i + 1), rankScore[i]);
        }
    }

    /// <summary>
    /// 최고 점수 구하기
    /// </summary>
    /// <returns> 가장 높은 점수 </returns>
    public int GetMaxScore()
    {
        if (rankScore.Count == 0)
            return 0;
        else
            return rankScore[0];
    }

    /// <summary>
    /// 지정된 순위의 점수를 반환
    /// </summary>
    /// <param name="rank">조회할 순위 (1~5) </param>
    /// <returns> 해당 순위의 점수 </returns>
    public int GetRank(int rank)
    {
        string key = "Rank";
        return PlayerPrefs.GetInt(key + rank);
    }

    /// <summary>
    /// 옵션 타입에 따른 값
    /// </summary>
    /// <param name="type"> 옵션 타입 </param>
    /// <returns> 옵션 값 </returns>
    public float GetSettingValue(OptionType type)
    {
        float result = 0f;

        switch (type)
        {
            case OptionType.Alpha:
                {
                    result = missileAlpha;
                    break;
                }
            case OptionType.Bgm:
                {
                    result = mainSound;
                    break;
                }
            case OptionType.EffectSound:
                {
                    result = effectSound;
                    break;
                }
        }

        return result;

    }

    /// <summary>
    /// 옵션 값 설정
    /// </summary>
    /// <param name="type"> 옵션 타입 </param>
    /// <param name="value"> 설정 값 </param>
    public void SetSettingValue(OptionType type, float value)
    {
        // 타입과 변수 매칭
        switch (type)
        {
            case OptionType.Alpha:
                missileAlpha = value;
                break;
            case OptionType.Bgm:
                mainSound = value;
                AudioController.Instance.SetBGMvolume(value);
                break;
            case OptionType.EffectSound:
                effectSound = value;
                break;
        }
    }

    /// <summary>
    /// PlayerPref로 저장해둔 데이터들을 전부 동기화 시키기 
    /// </summary>
    /// <remarks>
    /// 랭킹, 옵션
    /// </remarks>
    public void SyncPlayerPref()
    {
        if (gameDataKeys.Count == 0)
        {
            for (int i = 0; i < Enum.GetValues(typeof(GameDataKey)).Length; ++i)
            {
                GameDataKey key = (GameDataKey)i;
                string keyName = key.ToString();
                gameDataKeys.Add(keyName);
            }
        }

        if (PlayerPrefs.HasKey(gameDataKeys[(int)GameDataKey.MainSound]))
        {
            mainSound = PlayerPrefs.GetFloat(gameDataKeys[(int)GameDataKey.MainSound]);
        }

        if (PlayerPrefs.HasKey(gameDataKeys[(int)GameDataKey.EffectSound]))
        {
            effectSound = PlayerPrefs.GetFloat(gameDataKeys[(int)GameDataKey.EffectSound]);
        }

        if (PlayerPrefs.HasKey(gameDataKeys[(int)GameDataKey.MissileAlpha]))
        {
            missileAlpha = PlayerPrefs.GetFloat(gameDataKeys[(int)GameDataKey.MissileAlpha]);
        }

        for (int i = 1; i <= 5; ++i)
        {
            if (PlayerPrefs.HasKey(gameDataKeys[(int)GameDataKey.Rank] + i))
            {
                rankScore[i - 1] = (PlayerPrefs.GetInt(gameDataKeys[(int)GameDataKey.Rank] + i));
            }
        }

    }

    /// <summary>
    /// 시작했을 때, 옵션이 변경되었을때, PlayerPref도 갱신
    /// </summary>
    public void SetOption()
    {
        PlayerPrefs.SetFloat(gameDataKeys[(int)GameDataKey.MissileAlpha], missileAlpha);
        PlayerPrefs.SetFloat(gameDataKeys[(int)GameDataKey.MainSound], mainSound);
        PlayerPrefs.SetFloat(gameDataKeys[(int)GameDataKey.EffectSound], effectSound);
    }

    public Dictionary<string, ItemData> ItemDatas() => itemDatas;

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 바이너리 파일을 읽어서 데이터로 저장
    /// </summary>
    /// <remarks>
    /// Bytes 파일은 TextAsset으로 로드 가능
    /// 로드한 파일을 읽는 것이기 때문에, MemoryStream 사용
    /// 파일 내부가 Binary이기 때문에, BinaryReader 사용
    /// </remarks>
    private void LoadItemData()
    {
        string itemDataFilePath = "itemBinary";
        TextAsset itemBinaryData = Resources.Load<TextAsset>(itemDataFilePath);

        if(itemBinaryData == null)
        {
            Debug.LogError("Item binary File Missing");
        }

        using (MemoryStream memoryStream = new MemoryStream(itemBinaryData.bytes))
        {
            using (BinaryReader reader = new BinaryReader(memoryStream, Encoding.UTF8))
            { 
                int itemCount = reader.ReadInt32();

                for(int i = 0; i < itemCount; ++i)
                {
                    ItemData now = new ItemData();

                    now.Name = reader.ReadString();
                    now.Percent = reader.ReadString();
                    now.Value = reader.ReadString();
                    now.Time = reader.ReadString();
                
                    itemDatas.Add(now.Name, now);
                }
            }
        }
    }

    /// <summary>
    /// 미사일 데이터 로드
    /// </summary>
    private void LoadMissileData()
    {
        LoadFallingMissileData();
        LoadHoverMissileData();
        LoadGrandMissileData();
    }

    /// <summary>
    /// 낙하 미사일 바이너리 데이터를 읽어서 MissileSpawner에 전달
    /// </summary>
    private void LoadFallingMissileData()
    {
        string fallingMissile = "fallingMissileData";

        TextAsset fallingMissileBinaryData = Resources.Load<TextAsset>(fallingMissile);

        if (fallingMissileBinaryData == null)
        {
            Debug.LogError("Falling Missile Binary Missing");
            return;
        }

        FallingMissileSetting setting = new FallingMissileSetting();

        using (MemoryStream memoryStream = new MemoryStream(fallingMissileBinaryData.bytes))
        {
            using (BinaryReader reader = new BinaryReader(memoryStream, Encoding.UTF8))
            {
                setting.missileCount = reader.ReadInt32();
                setting.missileIncrement = reader.ReadInt32();
                setting.maxCount = reader.ReadInt32();

                setting.fallSpeed = reader.ReadSingle();
                setting.fallIncrement = reader.ReadSingle();
                setting.fallSpeedMax = reader.ReadSingle();

                setting.waiting = reader.ReadSingle();
                setting.waitIncrement = reader.ReadSingle();
                setting.waitingMax = reader.ReadSingle();
            }
        }

        MissileSpawner.Instance.FallingData = setting;
    }

    /// <summary>
    /// 추적 미사일 바이너리 데이터를 읽어서 MissileSpawner에 전달
    /// </summary>
    private void LoadHoverMissileData()
    {
        string hoverMissile = "hoverMissileData";

        TextAsset hoverMissileBinaryData = Resources.Load<TextAsset>(hoverMissile);

        if (hoverMissileBinaryData == null)
        {
            Debug.LogError("Hover Missile Binary Missing");
            return;
        }

        HoverMissileSetting setting = new HoverMissileSetting();

        using (MemoryStream memoryStream = new MemoryStream(hoverMissileBinaryData.bytes))
        {
            using (BinaryReader reader = new BinaryReader(memoryStream, Encoding.UTF8))
            {
                setting.hp = reader.ReadInt32();
                setting.hpIncrement = reader.ReadInt32();
                setting.hpMax = reader.ReadInt32();

                setting.flight = reader.ReadSingle();
                setting.flightIncrement = reader.ReadSingle();
                setting.flightMax = reader.ReadSingle();

                setting.flightSpeed = reader.ReadSingle();
                setting.flightSpeedIncrement = reader.ReadSingle();
                setting.flightSpeedMax = reader.ReadSingle();

                setting.turn = reader.ReadSingle();
                setting.turnIncrement = reader.ReadSingle();
                setting.turnMax = reader.ReadSingle();

                setting.turnRate = reader.ReadSingle();
                setting.turnRateIncrement = reader.ReadSingle();
                setting.turnRateMax = reader.ReadSingle();
            }
        }

        MissileSpawner.Instance.HoverData = setting;
    }

    /// <summary>
    /// Patterns 폴더의 모든 패턴 바이너리를 로드해 MissileSpawner에 전달.
    /// </summary>
    private void LoadMissilePatternData()
    {
        TextAsset[] patternAssets = Resources.LoadAll<TextAsset>("Patterns");
        if (patternAssets.Length == 0)
        {
            Debug.LogWarning("[GameData] Patterns 폴더에 패턴 파일이 없습니다.");
            return;
        }

        var patternDatas = new Dictionary<string, PatternData>();
        foreach (var asset in patternAssets)
        {
            PatternData data = LoadPatternFromAsset(asset);
            if (data != null)
                patternDatas[data.PatternName] = data;
        }

        MissileSpawner.Instance.PatternDatas = patternDatas;
        MissileSpawner.Instance.PatternNames = new List<string>(patternDatas.Keys);
        Debug.Log($"[GameData] 패턴 {patternDatas.Count}개 로드 완료. injected into MissileSpawner instId={MissileSpawner.Instance.GetInstanceID()} go={MissileSpawner.Instance.gameObject.name}");
    }

    private PatternData LoadPatternFromAsset(TextAsset asset)
    {
        using (var stream = new MemoryStream(asset.bytes))
        using (var reader = new BinaryReader(stream, Encoding.UTF8))
        {
            int fileType = reader.ReadInt32();
            if (fileType != 0) // 0 = Pattern (PatternSave.FileType)
            {
                Debug.LogError($"[GameData] 패턴 파일 타입 불일치: {asset.name}");
                return null;
            }

            reader.ReadInt32(); // version (추후 분기 처리)

            var data = new PatternData
            {
                PatternName   = reader.ReadString(),
                TotalDuration = reader.ReadSingle(),
                Events        = new List<PatternEvent>()
            };

            int evCount = reader.ReadInt32();
            for (int i = 0; i < evCount; i++)
                data.Events.Add(ReadPatternEvent(reader));

            return data;
        }
    }

    private PatternEvent ReadPatternEvent(BinaryReader reader)
    {
        var ev = new PatternEvent
        {
            Time      = reader.ReadSingle(),
            EventType = (PatternEventType)reader.ReadInt32(),
        };

        int missileCount = reader.ReadInt32();
        for (int j = 0; j < missileCount; j++)
        {
            int id = reader.ReadInt32();
            ev.LinkedMissileIds.Add(id);

            if (ev.EventType == PatternEventType.Spawn)
            {
                ev.SpawnInfos[id] = new SpawnInfo
                {
                    Type             = (PlacedMissileType)reader.ReadInt32(),
                    LocationKey      = reader.ReadString(),
                    OriginalPosition = new Vector3(
                        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
                };
            }

            if (ev.EventType != PatternEventType.Destroy)
                ev.StatsSnapshots[id] = ReadPatternSnapshot(reader);
        }

        return ev;
    }

    private MissileStatsSnapshot ReadPatternSnapshot(BinaryReader reader)
    {
        return new MissileStatsSnapshot
        {
            Speed          = reader.ReadSingle(),
            Hp             = reader.ReadInt32(),
            HoverType      = reader.ReadInt32(),
            FlightTime     = reader.ReadSingle(),
            TurnTime       = reader.ReadSingle(),
            TurnRate       = reader.ReadSingle(),
            GrandDiameter  = reader.ReadInt32(),
            GrandDirection = reader.ReadInt32(),
        };
    }

    /// <summary>
    /// 대형 미사일 바이너리 데이터를 읽어서 MissileSpawner에 전달
    /// </summary>
    private void LoadGrandMissileData()
    {
        string grandMissile = "grandMissileData";

        TextAsset grandMissileBinaryData = Resources.Load<TextAsset>(grandMissile);

        if (grandMissileBinaryData == null)
        {
            Debug.LogError("Grand Missile Binary Missing");
            return;
        }

        GrandMissileSetting setting = new GrandMissileSetting();

        using (MemoryStream memoryStream = new MemoryStream(grandMissileBinaryData.bytes))
        {
            using (BinaryReader reader = new BinaryReader(memoryStream, Encoding.UTF8))
            {
                setting.count = reader.ReadInt32();
                setting.countIncrement = reader.ReadInt32();
                setting.maxCount = reader.ReadInt32();

                setting.speed = reader.ReadSingle();
                setting.speedIncrement = reader.ReadSingle();
                setting.speedMax = reader.ReadSingle();

                setting.diameter = reader.ReadInt32();
                setting.diameterIncrement = reader.ReadInt32();
                setting.diameterMax = reader.ReadInt32();
            }
        }

        MissileSpawner.Instance.GrandData = setting;
    }

    #endregion
}
