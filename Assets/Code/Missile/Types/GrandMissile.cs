using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    [SerializeField] private float knockbackForce = 10f;

    [SerializeField] private int spawnIdx;

    [SerializeField] private GameObject[] warningDecals = new GameObject[2];
    [SerializeField] private DecalProjector[] decals = new DecalProjector[2];
    #endregion

    #region Private/Protected Fields

    private Transform modelTransform;
    private Vector3 baseScale;
    private Vector3 meshSize;

    private float dropPoint;
    private float platformY;
    private Vector3 tileScale;
    private const float decalYOffset = -0.5f;

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

        onDamage = false;

        // 자식 오브젝트에서 컴포넌트 캐싱 (base.Awake 결과를 자식 기준으로 덮어씀)
        col = GetComponentInChildren<Collider>();
        if (col != null) col.isTrigger = true;

        rb = GetComponentInChildren<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        MeshFilter meshFilter = GetComponentInChildren<MeshFilter>();
        modelTransform = meshFilter.transform;
        baseScale = modelTransform.localScale;
        meshSize = Vector3.Scale(meshFilter.sharedMesh.bounds.size, modelTransform.lossyScale);

        decals[0] = warningDecals[0].GetComponent<DecalProjector>();
        decals[1] = warningDecals[1].GetComponent<DecalProjector>();
    }

    private void Start()
    {
        dropPoint = GlobalData.Instance.MissileDropPoint - decalYOffset;
        platformY = MissileSpawner.Instance.gamePlatform.transform.position.y;
        tileScale = new Vector3(GlobalData.Instance.TileXScale, 1f, GlobalData.Instance.TileZScale);

        if (direction >= 1)
        {
            Vector3 platformPos = MissileSpawner.Instance.gamePlatform.transform.position;

            Vector3 fixPivot = decals[1].pivot;
            fixPivot.z = 1f;

            switch (direction)
            {
                case 1:
                warningDecals[1].transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                fixPivot.x = 15f;
                fixPivot.y = 0f;
                fixPivot.z = -1f;
                break;

                case 2:
                warningDecals[1].transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                fixPivot.x = 15f;
                fixPivot.y = 0f;
                break;

                case 3:
                warningDecals[1].transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                fixPivot.x = 15f;
                fixPivot.y = 0f;
                fixPivot.z = -1f;
                break;

                case 4:
                warningDecals[1].transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                fixPivot.x = 15f;
                fixPivot.y = 0f;
                break;
            }

            Vector3 decalSize = decals[1].size;

            switch(diameter)
            {
                case 2:
                    fixPivot.z *= 3f;
                    decalSize.y = 4f;
                    break;
                case 3:
                    fixPivot.z *= 4.5f;
                    decalSize.y = 6f;
                    break;
                case 4:
                    fixPivot.z *= 5.5f;
                    decalSize.y = 8f;
                    break;
                case 5:
                    fixPivot.z *= 6.5f;
                    decalSize.y = 10f;
                    break;
            }

            decals[1].pivot = fixPivot;
            decals[1].size = decalSize;

        }

        if(direction > 0)
        {
            warningDecals[0].SetActive(false);
            decals[0].enabled = false;
        }
        else
        {
            warningDecals[1].SetActive(false);
            decals[1].enabled = false;
        }

    }

    private void FixedUpdate()
    {
        ApplyVelocity();

        if (direction == 0)
        {
            UpdateVerticalDecal();
        }

    }

    private void OnEnable()
    {
        col.enabled = true;
        decals[0].size = Vector3.zero;
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
        modelTransform.localScale = new Vector3(
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

    #region Private Methods

    private void UpdateVerticalDecal()
    {
        warningDecals[0].transform.SetPositionAndRotation(
            new Vector3(transform.position.x, platformY + decalYOffset, transform.position.z),
            Quaternion.Euler(90f, 0f, 0f)
        );

        float currentHeight = transform.position.y - (platformY + decalYOffset);
        float scaleValue = 100f - ((currentHeight / dropPoint) * 100f);
        scaleValue = Mathf.Floor(scaleValue);
        scaleValue = Mathf.Clamp(scaleValue, 0f, 100f);

        float desiredSize = tileScale.x * diameter;

        decals[0].size = new Vector3(
            desiredSize * (scaleValue / 100f),
            desiredSize * (scaleValue / 100f),
            0.5f
        );

        Vector3 decalPivot = new Vector3(0f, 0f, -1.5f);
        decals[0].pivot = decalPivot;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("GameBoundary"))
        {
            Destroy(this.gameObject);
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
            Knockback(other);

        if (other.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            Vector3 contact = other.ClosestPoint(transform.position);
            ActiveBombEffect(contact, BoomParticle.Grand);
            Destroy(this.gameObject);
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Missile"))
        {
            GrandMissile otherGrand = other.GetComponentInParent<GrandMissile>();
            if (otherGrand != null)
            {
                Vector3 contact = other.ClosestPoint(transform.position);
                ActiveBombEffect(contact, BoomParticle.Grand);
                Destroy(this.gameObject);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
            Knockback(other);
    }

    private void Knockback(Collider other)
    {
        Player player = other.gameObject.GetComponent<Player>();

        Vector3 missileDir = physics.direction;
        Vector3 pushDir = (other.transform.position - transform.position).normalized;

        Vector3 knockbackDir = missileDir + pushDir;
        knockbackDir.y = 0f;
        knockbackDir = knockbackDir.normalized;

        player.ApplyKnockback(knockbackDir, diameter * 10f);
    }

    #endregion
}
