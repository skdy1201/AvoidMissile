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

    [SerializeField] private int spawnIdx;

    #endregion

    #region Private/Protected Fields

    private Vector3 baseScale;
    private Vector3 meshSize;

    #endregion

    #region Property

    public int SpawnIndex
    {
        set { spawnIdx = value; }
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        baseScale = transform.localScale;

        // 자식 오브젝트에서 컴포넌트 동기화 (base.Awake는 루트에서만 검색)
        if (col == null)
        {
            col = GetComponentInChildren<Collider>();
            if (col != null) col.isTrigger = true;
        }

        if (rb == null)
        {
            rb = GetComponentInChildren<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        MeshFilter meshFilter = GetComponentInChildren<MeshFilter>();
        meshSize = Vector3.Scale(meshFilter.sharedMesh.bounds.size, meshFilter.transform.lossyScale);
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

    /// <summary>
    /// 내부 스탯으로 physics, 크기, 회전 설정
    /// </summary>
    public override void Initialize()
    {
        // direction에 따른 이동 방향 결정
        // 0: Vertical (위→아래), 1: 북→남, 2: 남→북, 3: 동→서, 4: 서→동
        Vector3 moveDirection = direction switch
        {
            0 => Vector3.down,
            1 => -Vector3.forward,
            2 => Vector3.forward,
            3 => -Vector3.right,
            4 => Vector3.right,
            _ => Vector3.down
        };

        SetSpeed(speed, moveDirection);

        // 크기 설정 - 메시 실제 크기 기준으로 diameter 타일만큼 확대
        float tileSize = GlobalData.Instance.TileXScale;
        float desiredWidth = tileSize * diameter;
        float scaleFactor = desiredWidth / meshSize.x;
        transform.localScale = new Vector3(
            baseScale.x * scaleFactor,
            baseScale.y * scaleFactor * (2f / 3f),
            baseScale.z * scaleFactor
        );

        // 회전 설정 - 방향별 하드코딩
        transform.rotation = direction switch
        {
            0 => Quaternion.Euler(180f, 0f, 0f),
            1 => Quaternion.Euler(-90f, 0f, 0f),
            2 => Quaternion.Euler(90f, 0f, 0f),
            3 => Quaternion.Euler(0f, 0f, 90f),
            4 => Quaternion.Euler(0f, 0f, -90f),
            _ => Quaternion.Euler(180f, 0f, 0f)
        };
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
