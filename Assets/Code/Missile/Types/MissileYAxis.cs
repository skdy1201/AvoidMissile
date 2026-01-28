using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Y축 미사일 스크립트
/// </summary>
public class MissileYAxis : Missile
{
    #region Serialized Fields

    [SerializeField] public Vector2 XZCoord = new Vector2();
    [SerializeField] private Material missileMaterial;

    [SerializeField] private GameObject warningDecal;
    [SerializeField] private DecalProjector decal;
    [SerializeField] private float platformY;
    [SerializeField] private Vector3 tileScale = Vector3.zero;

    #endregion

    #region Private/Protected Fields

    private float dropPoint;

    private const float decalYOffset = 1.1f;

    #endregion

    #region Properties

    public bool MissileTransparent { get; set; } = false;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 콜라이더 컴포넌트 초기화, 충돌 설정을 위해 Awake에서 캐싱
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        missileCollider = GetComponent<Collider>();
        missileMaterial = GetComponent<MeshRenderer>().material;

        if (missileMaterial == null)
            Debug.LogError("missile material is null");

        if (warningDecal == null)
            Debug.LogError("missile Decal missing");

        decal = warningDecal.GetComponent<DecalProjector>();

        // Rigidbody 설정: 중력 비활성화 (직접 속도 제어)
        if (missileRigidbody != null)
        {
            missileRigidbody.useGravity = false;
            missileRigidbody.linearDamping = 0f;
        }
    }

    private void Start()
    {
        dropPoint = GlobalData.Instance.MissileDropPoint - decalYOffset;
        platformY = MissileSpawner.Instance.gamePlatform.transform.position.y;
        tileScale = new Vector3(GlobalData.Instance.TileXScale, 1f, GlobalData.Instance.TileZScale);
    }

    private void FixedUpdate()
    {
        // 속도 적용 (매 물리 프레임마다 일정한 속도 유지)
        ApplyVelocity();

        // 데칼 위치: 스폰된 XZ 좌표 + 플랫폼 높이
        Vector3 decalPoint = new Vector3(
            XZCoord.x,
            platformY + decalYOffset,
            XZCoord.y
        );
        warningDecal.transform.position = decalPoint;

        // 높이 기반 스케일 계산 (0~100)
        float currentHeight = transform.position.y - (platformY + decalYOffset);
        float scalevalue = 100f - (currentHeight / dropPoint * 100f);
        scalevalue = Mathf.Floor(scalevalue);
        scalevalue = Mathf.Clamp(scalevalue, 0f, 100f);

        decal.size = new Vector3(
            tileScale.x * (scalevalue / 100f),
            tileScale.z * (scalevalue / 100f),
            0.5f
            );
    }

    private void OnEnable()
    {
        missileCollider.enabled = true;
        decal.size = Vector3.zero;
    }

    private void OnDestroy()
    {
        Debug.Log("yaxis missile destroy");
    }

    #endregion

    #region Public Methods

    public float GetMissileAlpha() => missileMaterial.color.a;

    /// <summary>
    /// Y축 미사일 낙하 속도 초기화
    /// </summary>
    /// <param name="speed">낙하 속도 (units/second)</param>
    public override void Initialize(float speed)
    {
        SetSpeed(speed, Vector3.down);
    }

    /// <summary>
    /// 알파 값 변경 함수
    /// </summary>
    /// <param name="alphaValue">변경할 알파 값</param>
    public void ChangeAlpha(float alphaValue)
    {
        // 아직 머티리얼이 설정되지 않았다면
        if (missileMaterial == null)
            return;

        missileMaterial.color = new Color(missileMaterial.color.r, missileMaterial.color.g, missileMaterial.color.b, alphaValue);
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// Y축 미사일 충돌 처리
    /// </summary>
    /// <param name="collision">충돌한 오브젝트의 Collision 정보</param>
    protected override void OnCollisionEnter(Collision collision)
    {
        // 이미 충돌했는지 확인
        if (missileCollider.enabled == false)
            return;

        base.OnCollisionEnter(collision);

        // 폭발 이펙트를 정확한 충돌 위치에 표시하기 위해 좌표 저장
        Vector3 contact = collision.contacts[0].point;

        ActiveBombEffect(contact);

        // 충돌체의 레이어에 따라 분기
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            this.missileCollider.enabled = false;

            MissileSpawner.Instance.ReturnSpawner(MissileType.YAxis, this.gameObject);

            GameProgress.Instance.Score = GameProgress.Instance.Score;
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("Missile"))
        {
            // Y축 미사일과 충돌했다면 두 미사일의 Y 값을 비교해서 위에 있으면 풀 반환
            // 더 높이 있는 미사일이라면 아래 미사일이 더 빨리 떨어지게
            if (collision.gameObject.GetComponent<MissileYAxis>() != null)
            {
                float otherMissileY = collision.gameObject.transform.position.y;

                if (this.gameObject.transform.position.y > otherMissileY)
                {
                    MissileSpawner.Instance.ReturnSpawner(MissileType.YAxis, this.gameObject);
                }
                else
                {
                    // 낙하 속도 증가 (최대 15 units/second까지)
                    physics.speed = Mathf.Min(physics.speed + 1f, 15f);
                }
            }
            else if (collision.gameObject.GetComponent<MissileXAxis>() != null)
            {
                MissileSpawner.Instance.ReturnSpawner(MissileType.YAxis, this.gameObject);
            }
        }
    }

    #endregion

}
