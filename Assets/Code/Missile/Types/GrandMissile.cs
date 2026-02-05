using UnityEngine;

public enum GrandMissileType
{
    Vertical,
    Horizen,
};

/// <summary>
/// 바이너리 파일로 변환한 레벨 별 대형 미사일 세팅
/// </summary>
[System.Serializable]
public struct GrandMissileSetting
{
    [Header("Count")]
    [SerializeField] public int count;
    [SerializeField] public int countIncrement;
    [SerializeField] public int maxCount;

    [Header("Speed")]
    [SerializeField] public float speed;
    [SerializeField] public float speedIncrement;
    [SerializeField] public float speedMax;

    [Header("Diameter")]
    [SerializeField] public int diameter;
    [SerializeField] public int diameterIncrement;
    [SerializeField] public int diameterMax;
};

public class GrandMissile : Missile
{
    #region Serialize Field

    [SerializeField] private GrandMissileType type;
    [SerializeField] private float speed;
    [SerializeField] private int diameter;
    [SerializeField] private int direction;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

    }

    private void OnEnable()
    {
        
    }

    private void Start()
    {
        
    }

    private void FixedUpdate()
    {
        // 속도 적용 (매 물리 프레임마다 일정한 속도 유지)
        ApplyVelocity();
    }

    #endregion

    #region Public Methods

    public override void Initialize(float speed)
    {
        //SetSpeed(speed, Vector3.down);
    }

    /// <summary>
    /// 대형 미사일 스탯 설정
    /// </summary>
    /// <param name="randomType">미사일 타입</param>
    /// <param name="randomSpeed">속도</param>
    /// <param name="randomDiameter">직경</param>
    /// <param name="randomDirection">방향 (0: Vertical, 1: 북, 2: 남, 3: 동, 4: 서)</param>
    public void SetStat(GrandMissileType randomType, float randomSpeed, int randomDiameter, int randomDirection = 0)
    {
        type = randomType;
        speed = randomSpeed;
        diameter = randomDiameter;
        direction = randomDirection;
    }    

    #endregion
}
