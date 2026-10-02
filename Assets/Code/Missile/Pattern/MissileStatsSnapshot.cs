/// <summary>특정 시점의 미사일 스탯 스냅샷.</summary>
/// <remarks> 미사일 스탯이 좀 더 늘어나면 상속을 고려할 것 </remarks>
public class MissileStatsSnapshot
{
    public float Speed;
    public int Hp;
    public int HoverType;   // HoverMissileType enum int
    public float FlightTime;
    public float TurnTime;
    public float TurnRate;
    public int GrandDiameter;
    public int GrandDirection;

    /// <summary>
    /// 미사일 홀더의 값을 읽어온다.
    /// </summary>
    public static MissileStatsSnapshot FromHolder(MissileStatHolder h)
    {
        return new MissileStatsSnapshot
        {
            Speed          = h.speed,
            Hp             = h.hp,
            HoverType      = (int)h.hoverType,
            FlightTime     = h.flightTime,
            TurnTime       = h.turnTime,
            TurnRate       = h.turnRate,
            GrandDiameter  = h.grandDiameter,
            GrandDirection = h.grandDirection,
        };
    }

    /// <summary>
    /// 미사일 홀더에 현재 설정 값을 적용한다.
    /// </summary>
    public void ApplyToHolder(MissileStatHolder h)
    {
        h.speed          = Speed;
        h.hp             = Hp;
        h.hoverType      = (HoverMissileType)HoverType;
        h.flightTime     = FlightTime;
        h.turnTime       = TurnTime;
        h.turnRate       = TurnRate;
        h.grandDiameter  = GrandDiameter;
        h.grandDirection = GrandDirection;
    }
}
