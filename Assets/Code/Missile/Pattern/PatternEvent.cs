using System.Collections.Generic;

public enum PatternEventType { Spawn, StatChange, Destroy }

/// <summary>
/// 패턴 이벤트는 생성, 스탯 변경, 파괴 3가지 타입이 존재
/// 이벤트가 적용될 시간, 종류, 연결된 미사일 id, 스냅샷 정보가 들어가 있다.
/// </summary>
public class PatternEvent
{
    public float Time;
    public PatternEventType EventType;
    public List<int> LinkedMissileIds = new List<int>();

    /// <summary>Spawn/StatChange 이벤트에 저장된 미사일별 스탯 스냅샷.</summary>
    public Dictionary<int, MissileStatsSnapshot> StatsSnapshots = new Dictionary<int, MissileStatsSnapshot>();
}
