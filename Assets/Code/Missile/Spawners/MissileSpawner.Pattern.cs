using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// MissileSpawner의 패턴 실행 담당 partial class.
/// 패턴 데이터 보관, 이벤트 순서 처리, 미사일 직접 스폰/스탯변경을 수행한다.
/// </summary>
public partial class MissileSpawner
{
    #region Private/Protected Fields

    private enum PatternPhase { None, PreDelay, Playing, PostDelay }

    private Dictionary<string, PatternData> patternDatas;
    private List<string> patternNames = new List<string>();
    private PatternData currentPattern;
    private int patternIdx;
    private float patternTimer;

    private float patternDelayTimer;
    private const float PatternPreDelay = 3f;
    private const float PatternPostDelay = 3f;

    /// <summary>패턴 이벤트로 스폰된 미사일 — ID → GameObject O(1) 조회</summary>
    private Dictionary<int, GameObject> activePatternMissiles = new Dictionary<int, GameObject>();

    #endregion

    #region Properties

    /// <summary>
    /// 패턴 데이터 설정 (GameData에서 로드 후 주입)
    /// </summary>
    public Dictionary<string, PatternData> PatternDatas
    {
        set { patternDatas = value; }
    }

    public List<string> PatternNames
    {
        set {patternNames = value; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 패턴 진입 준비: 랜덤 스폰 정지 → 활성 미사일 제거 → PreDelay 타이머 시작
    /// </summary>
    public void PrepareToPattern()
    {
        spawning = false;

        DestroyAllActiveMissiles();

        patternPhase = PatternPhase.PreDelay;
        patternDelayTimer = PatternPreDelay;

        Debug.Log("[Pattern] PrepareToPattern → PreDelay 시작");
    }

    /// <summary>
    /// 패턴을 시작한다. 이름이 null이면 랜덤 선택.
    /// </summary>
    public void SpawnPattern(string patternName = null)
    {
        Debug.Log($"[SpawnPattern] 진입 patternName={patternName} instId={GetInstanceID()} go={gameObject.name}");

        if (patternDatas == null || patternDatas.Count == 0)
        {
            Debug.LogWarning($"[SpawnPattern] 패턴 데이터 없음 instId={GetInstanceID()}");
            return;
        }

        Debug.Log($"[SpawnPattern] patternDatas keys = [{string.Join(",", patternDatas.Keys)}]");
        Debug.Log($"[SpawnPattern] patternNames     = [{string.Join(",", patternNames)}]");

        if (patternName != null && patternDatas.TryGetValue(patternName, out PatternData named))
        {
            Debug.Log("[SpawnPattern] matched first branch");
            currentPattern = named;
        }
        else
        {
            Debug.Log("[SpawnPattern] fell into else branch");
            string fallbackName = patternNames[Random.Range(0, patternNames.Count)];
            Debug.Log($"[SpawnPattern] else branch fallbackName={fallbackName}, contained={patternDatas.ContainsKey(fallbackName)}");
            currentPattern = patternDatas[fallbackName];
        }

        Debug.Log("[SpawnPattern] about to set Playing");
        patternIdx   = 0;
        patternTimer = 0f;
        patternPhase = PatternPhase.Playing;

        Debug.Log($"[SpawnPattern] 패턴 시작: {currentPattern.PatternName}");
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// 매 프레임 타이머를 올리고, 해당 시간에 도달한 이벤트를 순서대로 실행.
    /// </summary>
    private void TickPattern()
    {
        if (patternPhase != PatternPhase.Playing) return;

        Debug.Log("in pattern play");
        patternTimer += Time.deltaTime;

        while (patternIdx < currentPattern.Events.Count &&
               currentPattern.Events[patternIdx].Time <= patternTimer)
        {
            ExecutePatternEvent(patternIdx);
            patternIdx++;
        }

        if (patternIdx >= currentPattern.Events.Count &&
            patternTimer >= currentPattern.TotalDuration)
        {
            patternPhase = PatternPhase.PostDelay;
            patternDelayTimer = PatternPostDelay;
            Debug.Log($"[Pattern] 패턴 종료 → PostDelay: {currentPattern.PatternName}");
            Debug.Log($"[Pattern] 패턴 종료 시간 {currentPattern.TotalDuration}");
            
        }
    }

    private void ExecutePatternEvent(int idx)
    {
        PatternEvent patternEvent = currentPattern.Events[idx];
        switch (patternEvent.EventType)
        {
            case PatternEventType.Spawn:
                foreach (int missileId in patternEvent.LinkedMissileIds)
                {
                    if (!patternEvent.SpawnInfos.TryGetValue(missileId, out SpawnInfo info)) continue;
                    patternEvent.StatsSnapshots.TryGetValue(missileId, out MissileStatsSnapshot snap);
                    SpawnPatternMissile(missileId, info, snap);
                }
                break;

            case PatternEventType.StatChange:
                ChangeStat(patternEvent);
                break;

            case PatternEventType.Destroy:
                // 미구현
                break;
        }
    }

    private void SpawnPatternMissile(int missileId, SpawnInfo info, MissileStatsSnapshot snap)
    {
        if (snap == null) snap = new MissileStatsSnapshot();

        switch (info.Type)
        {
            case PlacedMissileType.Falling: SpawnPatternFalling(missileId, info, snap); break;
            case PlacedMissileType.Hover:   SpawnPatternHover(missileId, info, snap);   break;
            case PlacedMissileType.Grand:   SpawnPatternGrand(missileId, info, snap);   break;
        }
    }

    private void SpawnPatternFalling(int missileId, SpawnInfo info, MissileStatsSnapshot snap)
    {
        GameObject obj = RentSpawner(MissileType.Falling);
        if (obj == null) return;

        obj.layer = LayerMask.NameToLayer("PatternMissile");

        Vector3 pos = ResolveSpawnPosition(info);
        Vector3 spawnPos = pos;
        spawnPos.y = GlobalData.Instance.MissileDropPoint;

        FallingMissile missile = obj.GetComponent<FallingMissile>();
        missile.XZCoord = new Vector2(pos.x, pos.z);
        missile.SetStat(snap.Speed);
        obj.transform.position = spawnPos;
        missile.Initialize();
        obj.SetActive(true);

        currentFallingMissiles.AddLast(obj);
        activePatternMissiles[missileId] = obj;
    }

    private void SpawnPatternHover(int missileId, SpawnInfo info, MissileStatsSnapshot snap)
    {
        Vector3 pos = ResolveSpawnPosition(info);
        pos.y += 2.5f;

        GameObject obj = Instantiate(hoverMissilePrefab);
        obj.transform.position = pos;
        obj.layer = LayerMask.NameToLayer("PatternMissile");

        HoverMissile missile = obj.GetComponent<HoverMissile>();
        missile.ApplySnapshot(snap);
        missile.SetSpawnDirection(GetSpawnDirection(info.LocationKey));

        currentHoverMissiles.AddLast(obj);
        activePatternMissiles[missileId] = obj;
    }

    private void SpawnPatternGrand(int missileId, SpawnInfo info, MissileStatsSnapshot snap)
    {
        int grandDir = snap.GrandDirection;
        GrandMissileType type = grandDir == 0 ? GrandMissileType.Vertical : GrandMissileType.Horizen;

        Vector3 pos = ResolveSpawnPosition(info);

        if (type == GrandMissileType.Vertical)
        {
            pos.y = GlobalData.Instance.MissileDropPoint;
        }
        else
        {
            pos.y += snap.GrandDiameter switch
            {
                2 => 4f, 3 => 5.5f, 4 => 6.5f, 5 => 7.5f, _ => 4f
            };
        }

        GameObject obj = Instantiate(grandMissilePrefab);
        obj.transform.position = pos;

        int patternLayer = LayerMask.NameToLayer("PatternMissile");
        obj.layer = patternLayer;
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = patternLayer;

        GrandMissile missile = obj.GetComponent<GrandMissile>();
        missile.SetStat(type, snap.Speed, snap.GrandDiameter, grandDir);
        missile.Initialize();

        currentGrnadMissiles.AddLast(obj);
        activePatternMissiles[missileId] = obj;
    }

    private void ChangeStat(PatternEvent patternEvent)
    {
        foreach (int missileId in patternEvent.LinkedMissileIds)
        {
            if (!activePatternMissiles.TryGetValue(missileId, out GameObject obj) || obj == null) continue;
            if (!patternEvent.StatsSnapshots.TryGetValue(missileId, out MissileStatsSnapshot snap)) continue;

            FallingMissile falling = obj.GetComponent<FallingMissile>();
            if (falling != null) { falling.SetStat(snap.Speed); continue; }

            HoverMissile hover = obj.GetComponent<HoverMissile>();
            if (hover != null)
            {
                hover.SetStat(snap.Hp, snap.FlightTime, snap.Speed, snap.TurnTime, snap.TurnRate);
                continue;
            }

            GrandMissile grand = obj.GetComponent<GrandMissile>();
            if (grand != null)
            {
                int grandDir = snap.GrandDirection;
                GrandMissileType grandType = grandDir == 0 ? GrandMissileType.Vertical : GrandMissileType.Horizen;
                grand.SetStat(grandType, snap.Speed, snap.GrandDiameter, grandDir);
            }
        }
    }

    private string RandomPattern()
    {
        return patternNames[Random.Range(0, patternNames.Count)];    
    }

    /// <summary>
    /// PatternPhase에 따라 preDelay / postDelay 타이머를 소비한다.
    /// </summary>
    private void TickPatternPhase()
    {
        if (patternPhase == PatternPhase.None) return;

        if (patternPhase == PatternPhase.PreDelay)
        {
            patternDelayTimer -= Time.deltaTime;

            if (patternDelayTimer <= 0f)
            {
                string patternName = RandomPattern();
                SpawnPattern(patternName);
            }
        }
        else if (patternPhase == PatternPhase.Playing)
        {
            TickPattern();
        }
        else if (patternPhase == PatternPhase.PostDelay)
        {
            patternDelayTimer -= Time.deltaTime;
            if (patternDelayTimer <= 0f)
            {
                patternPhase = PatternPhase.None;
                spawning = true;
                patternReady = false;
                readyPatternTimer = patternCycleCooldown;
                Debug.Log("[Pattern] PostDelay 종료 → 랜덤 스폰 재개");
            }
        }
    }

    /// <summary>
    /// 씬 내 모든 활성 미사일을 일괄 제거한다.
    /// </summary>
    private void DestroyAllActiveMissiles()
    {
        // Falling: 풀 반환
        while (currentFallingMissiles.Count > 0)
        {
            GameObject obj = currentFallingMissiles.First.Value;
            if (obj != null)
                ReturnSpawner(MissileType.Falling, obj);
            else
                currentFallingMissiles.RemoveFirst();
        }

        // Hover: Instantiate 기반 → Destroy
        foreach (GameObject obj in currentHoverMissiles)
        {
            if (obj != null)
                Destroy(obj);
        }
        currentHoverMissiles.Clear();

        // Grand: Instantiate 기반 → Destroy
        foreach (GameObject obj in currentGrnadMissiles)
        {
            if (obj != null)
                Destroy(obj);
        }
        currentGrnadMissiles.Clear();

        activePatternMissiles.Clear();
    }

    /// <summary>
    /// LocationKey를 런타임 월드 좌표로 변환한다. 실패 시 OriginalPosition 반환.
    /// </summary>
    private Vector3 ResolveSpawnPosition(SpawnInfo info)
    {
        string key = info.LocationKey;

        if (key.StartsWith("T:"))
        {
            var parts = key[2..].Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int col) &&
                int.TryParse(parts[1], out int row))
            {
                GameObject tile = gamePlatform.GetTile(row * 10 + col);
                if (tile != null)
                {
                    Vector3 pos = tile.transform.position;
                    pos.x -= tileX / 2f;
                    pos.z += tileZ / 2f;
                    return pos;
                }
            }
        }
        else if (key.StartsWith("S:"))
        {
            return SpawnIdToPosition(key.Substring(2));
        }

        Debug.LogWarning($"[Pattern] LocationKey 해석 실패: {key}, OriginalPosition 사용");
        return info.OriginalPosition;
    }

    private Vector3 SpawnIdToPosition(string spawnId)
    {
        if (spawnId.StartsWith("N:") && int.TryParse(spawnId.Substring(2), out int ni))
            return spawnPointGroup.GetPointPosition(0, ni);
        if (spawnId.StartsWith("S:") && int.TryParse(spawnId.Substring(2), out int si))
            return spawnPointGroup.GetPointPosition(1, si);
        if (spawnId.StartsWith("E:") && int.TryParse(spawnId.Substring(2), out int ei))
            return spawnPointGroup.GetPointPosition(2, ei);
        if (spawnId.StartsWith("W:") && int.TryParse(spawnId.Substring(2), out int wi))
            return spawnPointGroup.GetPointPosition(3, wi);

        return spawnId switch
        {
            "NE" => spawnPointGroup.GetPointPosition(4, 0),
            "NW" => spawnPointGroup.GetPointPosition(5, 0),
            "SE" => spawnPointGroup.GetPointPosition(6, 0),
            "SW" => spawnPointGroup.GetPointPosition(7, 0),
            _    => Vector3.zero
        };
    }

    /// <summary>
    /// LocationKey에서 HoverMissile 스폰 방향(0=N,1=S,2=E,3=W,4~7=대각)을 추출.
    /// 스폰 포인트가 아니거나 알 수 없는 경우 -1 반환.
    /// </summary>
    private int GetSpawnDirection(string locationKey)
    {
        if (!locationKey.StartsWith("S:")) return -1;
        string spawnId = locationKey.Substring(2);
        if (spawnId.StartsWith("N:")) return 0;
        if (spawnId.StartsWith("S:")) return 1;
        if (spawnId.StartsWith("E:")) return 2;
        if (spawnId.StartsWith("W:")) return 3;
        // 스폰 코너 → 판 안쪽(반대 코너)으로 이동. 에디터 GetSpawnFacingDirection과 일치.
        return spawnId switch { "NE" => 4, "NW" => 5, "SE" => 6, "SW" => 7, _ => -1 };
    }

    #endregion
}
