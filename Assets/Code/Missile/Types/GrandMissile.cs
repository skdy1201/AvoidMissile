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

    [Header("Size")]
    [SerializeField] public int size;
    [SerializeField] public int sizeIncrement;
    [SerializeField] public int sizeMax;
};

public class GrandMissile : Missile
{
    #region Serialize Field

    [SerializeField] private GrandMissileType type;
    [SerializeField] private float speed;
    [SerializeField] private int size;

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
    #endregion
}
