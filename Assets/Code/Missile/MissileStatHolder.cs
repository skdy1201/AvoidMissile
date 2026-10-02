using UnityEngine;

/// <summary>
/// 패턴 에디터에서 배치된 미사일 ghost에 부착되는 스탯 컨테이너.
/// 커스텀 인스펙터(MissileStatHolderEditor)로 타입별 필드를 렌더링한다.
/// 런타임에서는 사용되지 않음 — ghost가 HideFlags.DontSave로 생성됨.
/// </summary>
public class MissileStatHolder : MonoBehaviour
{
    [HideInInspector] public int missileId;
    [HideInInspector] public PlacedMissileType missileType;

    // 공통
    public float speed = 1f;

    // Hover 전용
    public int            hp        = 1;
    public HoverMissileType hoverType = HoverMissileType.HorizonLinear;

    // Hover 유도 전용 (hoverType이 Custom/Real일 때만 의미)
    public float flightTime = 2f;
    public float turnTime   = 5f;
    public float turnRate   = 90f;

    // Grand 전용
    public int grandDiameter  = 3;
    public int grandDirection = 0; // 0=Vertical, 1=N→S, 2=S→N, 3=E→W, 4=W→E
}
