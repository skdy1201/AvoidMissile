using System.Collections.Generic;

/// <summary>
///  로드된 패턴 데이터. 이름, 총 시간, 이벤트 리스트를 포함.
/// </summary>
public class PatternData
{
    public string PatternName;
    public float TotalDuration;
    public List<PatternEvent> Events;
}
