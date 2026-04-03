using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MissileSpawner의 패턴 실행 담당 partial class.
/// 패턴 데이터 보관, 이벤트 큐 처리, 미사일 직접 스폰/스탯변경/파괴를 수행한다.
/// </summary>
public partial class MissileSpawner
{
    #region Private/Protected Fields

    private Dictionary<string, PatternData> patternDatas;
    private Queue<PatternEvent> patternEventQueue;
    private float patternElapsedTime;
    private bool patternRunning;

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

    #endregion
}
