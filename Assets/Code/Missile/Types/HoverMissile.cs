using System.Collections;
using UnityEngine;

/// <summary>
/// 바이너리 파일로 변환한 레벨 별 추적 미사일 세팅
/// </summary>
[System.Serializable]
public struct HoverMissileSetting
{
    [Header("HP")]
    [SerializeField] public int hp;
    [SerializeField] public int hpIncrement;
    [SerializeField] public int hpMax;

    [Header("Flight")]
    [SerializeField] public float flight;
    [SerializeField] public float flightIncrement;
    [SerializeField] public float flightMax;

    [Header("FlightSpeed")]
    [SerializeField] public float flightSpeed;
    [SerializeField] public float flightSpeedIncrement;
    [SerializeField] public float flightSpeedMax;

    [Header("Turn")]
    [SerializeField] public float turn;
    [SerializeField] public float turnIncrement;
    [SerializeField] public float turnMax;

    [Header("TurnRate")]
    [SerializeField] public float turnRate;
    [SerializeField] public float turnRateIncrement;
    [SerializeField] public float turnRateMax;
}

public enum HoverMissileType
{
    Custom,
    Real,
    HorizonLinear
}

/// <summary>
/// 추적 미사일 스크립트
/// </summary>
/// <remarks>
/// 현실의 유도 미사일, 이동 - 회전을 나눠 움직이는 미사일의 종류로 나뉨
/// </remarks>
public class HoverMissile : Missile
{
    #region Serialized Fields

    [Header("Stat")]
    [SerializeField] private int hp;
    [SerializeField] private float maxMoveTime;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float rotateTime;
    [SerializeField] private float rotateSpeed;
    [SerializeField] private HoverMissileType hoverType;

    #endregion

    #region Private/Protected Fields

    private IMovementStrategy strategy;
    private int spawnDirection = -1;

    #endregion

    #region Properties

    // 전략 클래스에서 스탯을 읽기 위한 접근자
    public float MoveSpeed => moveSpeed;
    public float MaxMoveTime => maxMoveTime;
    public float RotateSpeed => rotateSpeed;
    public float RotateTime => rotateTime;

    // 전략 클래스에서 Transform 정보를 읽기 위한 접근자
    public Vector3 Position => transform.position;
    public Vector3 Forward => transform.forward;
    public Quaternion Rotation
    {
        get => transform.rotation;
        set => transform.rotation = value;
    }

    public Vector3 PlayerPosition => GlobalData.Instance.Player.transform.position;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        // Rigidbody 설정은 부모 클래스(Missile)에서 isKinematic = true로 처리
        // transform 직접 이동 방식이므로 constraints 대신 코드에서 Y축 고정
    }

    /// <summary>
    /// HorizonLinear 타입일 때 스폰 방향으로 회전 설정 후 physics 동기화, 전략 할당.
    /// 스탯은 스포너가 Instantiate 직후 ApplySnapshot 또는 GetRandomSettingHoming으로 주입.
    /// </summary>
    void Start()
    {
        // HorizonLinear는 spawnDirection이 가리키는 방향으로 직선 이동
        if (hoverType == HoverMissileType.HorizonLinear && spawnDirection >= 0)
        {
            // 직선: 0=N→S, 1=S→N, 2=E→W, 3=W→E
            // 대각선: 4=SE, 5=SW, 6=NE, 7=NW
            Vector3 dir = spawnDirection switch
            {
                0 => Vector3.back,
                1 => Vector3.forward,
                2 => Vector3.left,
                3 => Vector3.right,
                4 => new Vector3(+1f, 0f, -1f).normalized,
                5 => new Vector3(-1f, 0f, -1f).normalized,
                6 => new Vector3(+1f, 0f, +1f).normalized,
                7 => new Vector3(-1f, 0f, +1f).normalized,
                _ => Vector3.back
            };
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        Initialize();
        AssignStrategy();
    }

    private void FixedUpdate()
    {
        strategy.Execute(this);
    }
    #endregion

    #region Public Methods

    /// <summary>
    /// 내부 스탯으로 physics 설정
    /// </summary>
    public override void Initialize()
    {
        physics.speed = moveSpeed;
        physics.direction = transform.forward;
    }

    /// <summary>
    /// 이동 방향을 설정하고 속도를 적용 (전략 클래스용)
    /// </summary>
    public void UpdateDirection(Vector3 direction)
    {
        physics.direction = direction;
    }

    /// <summary>
    /// 현재 physics 설정으로 이동 적용 (전략 클래스용)
    /// </summary>
    public void ApplyMovement()
    {
        ApplyVelocity();
    }

    /// <summary>
    /// 코루틴 시작 래퍼 (전략 클래스용)
    /// </summary>
    public Coroutine StartMissileCoroutine(IEnumerator routine)
    {
        return StartCoroutine(routine);
    }

    /// <summary>
    /// 스폰 방향 설정 (0=N, 1=S, 2=E, 3=W)
    /// </summary>
    public void SetHoverType(HoverMissileType type) => hoverType = type;
    public void SetSpawnDirection(int direction) => spawnDirection = direction;

    /// <summary>
    /// 스냅샷의 스탯을 미사일에 적용한다. 패턴/랜덤 모두 이 경로를 사용.
    /// </summary>
    public void ApplySnapshot(MissileStatsSnapshot snap)
    {
        hoverType = (HoverMissileType)snap.HoverType;
        SetStat(snap.Hp, snap.FlightTime, snap.Speed, snap.TurnTime, snap.TurnRate);
    }

    public void SetHP(int HP) => hp = HP;
    public void SetMoveTime(float time) => maxMoveTime = time;
    public void SetMoveSpeed(float speed) => moveSpeed = speed;
    public void SetRotateTime(float time) => rotateTime = time;
    public void SetRotateSpeed(float speed) => rotateSpeed = speed;

    /// <summary>
    /// 미사일 스탯 설정
    /// </summary>
    /// <param name="hp"> 미사일 체력 </param>
    /// <param name="moveTime"> 이동 시간 </param>
    /// <param name="moveSpeed"> 이동 속도 </param>
    /// <param name="rotateTime"> 회전 시간 </param>
    /// <param name="rotateSpeed"> 회전 속도 </param>
    public void SetStat(int hp, float moveTime, float moveSpeed, float rotateTime, float rotateSpeed)
    {
        SetHP(hp);
        SetMoveTime(moveTime);
        SetMoveSpeed(moveSpeed);
        SetRotateTime(rotateTime);
        SetRotateSpeed(rotateSpeed);
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 낙하 미사일과 맞으면 HP 감소. 0이 되면 소멸.
    /// 충돌 지점에 폭발 효과
    /// </summary>
    /// <param name="other"> 충돌 물체의 Collider </param>
    /// <remarks>
    /// 미사일의 요소만 관리
    /// 플레이어의 체력 감소는 플레이어가 담당
    /// </remarks>
    private void OnTriggerEnter(Collider other)
    {
        // GameBoundary 충돌 시 제거 (HorizonLinear 등 맵 밖으로 나가는 경우)
        if (other.gameObject.layer == LayerMask.NameToLayer("GameBoundary"))
        {
            MissileSpawner.Instance.RemoveHoverMissile(gameObject);
            Destroy(gameObject);
            return;
        }

        // PowerJump 상태의 Player와 충돌 시 미사일 파괴
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            Player player = other.gameObject.GetComponent<Player>();

            if (player.ActivePowerJump)
            {
                GameProgress.Instance.Score = GameProgress.Instance.Score;

                Vector3 contact = other.ClosestPoint(transform.position);
                ActiveBombEffect(contact);

                gameObject.SetActive(false);
                MissileSpawner.Instance.RemoveHoverMissile(gameObject);
                Destroy(gameObject);
            }
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Missile"))
        {
            if (other.gameObject.GetComponent<FallingMissile>() != null)
            {
                hp--;

                if (hp <= 0)
                {
                    MissileSpawner.Instance.RemoveHoverMissile(gameObject);
                    Destroy(gameObject);
                }

                Vector3 contact = other.ClosestPoint(transform.position);
                ActiveBombEffect(contact);
            }
            else if (other.GetComponentInParent<GrandMissile>() != null)
            {
                MissileSpawner.Instance.RemoveHoverMissile(gameObject);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// hoverType에 따라 이동 전략 할당
    /// </summary>
    private void AssignStrategy()
    {
        strategy = hoverType switch
        {
            HoverMissileType.Custom => new CustomHomingStrategy(),
            HoverMissileType.Real => new RealHomingStrategy(),
            HoverMissileType.HorizonLinear => new HorizonLinearStrategy(),
            _ => new RealHomingStrategy()
        };
    }

    #endregion

}
