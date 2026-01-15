using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Y축 미사일 스크립트
/// </summary>
public class MissileYAxis : Missile
{
    #region Serialized Fields
    
    [SerializeField] public Vector2 XZCoord = new Vector2();
    [SerializeField] private Material missileMaterial;

    [SerializeField] private GameObject warningDecal;

    #endregion

    #region Private/Protected Fields

    private RaycastHit[] raycastResult = new RaycastHit[1];

    private float dropPoint;

    private int layerMask;

    [SerializeField] private Vector3 tileScale = Vector3.zero;
    #endregion

    #region Properties

    public bool MissileTransparent { get; set; } = false;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 콜라이더 컴포넌트 초기화. 충돌 감지를 위해 Awake에서 캐싱
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

    }

    private void Start()
    {
        dropPoint = GlobalData.Instance.MissileDropPoint;
        layerMask = 1 << LayerMask.NameToLayer("Platform");
        tileScale = new Vector3(GlobalData.Instance.TileXScale, 1f, GlobalData.Instance.TileZScale);
    }

    private void FixedUpdate()
    {
        Vector3 position = transform.position;

        Vector3 downDirection = Vector3.down;

        Debug.DrawRay(position, downDirection, Color.yellow);


        int hitCount = Physics.RaycastNonAlloc(this.gameObject.transform.position, downDirection, raycastResult, 999, layerMask, QueryTriggerInteraction.Collide);

        float missileheight = gameObject.transform.position.y;


        if (hitCount > 0)
        {
            Debug.Log("collider in tile");

            Vector3 decalPoint = raycastResult[0].point;
            decalPoint.y += 0.15f;
            warningDecal.transform.position = decalPoint;

            float ratio = 100 - (transform.position.y / dropPoint * 100);

            float scalevalue = ratio * (1f / 100f);
            scalevalue = Mathf.Floor(scalevalue * 100f) / 100f;
            scalevalue = Mathf.Clamp(scalevalue, 0, 1);

            Vector3 parentLossyScale = transform.lossyScale;

            warningDecal.transform.localScale = new Vector3(
                (tileScale.x * scalevalue) / parentLossyScale.x,  // X 독립 보정
                1f,                                                // Y 고정
                (tileScale.z * scalevalue) / parentLossyScale.z   // Z 독립 보정
            );

        }
        else
        {
            // Y축 미사일이 없으면 데칼 제거
            warningDecal.transform.localScale = Vector3.zero;
        }
    }

    private void OnEnable()
    {
        this.CollisionOther = false;
    }

    private void OnDestroy()
    {
        Debug.Log("yaxis missile destroy");
    }

    #endregion

    #region Public Methods

    public float GetMissileAlpha() => missileMaterial.color.a;

    /// <summary>
    /// 미사일의 재질을 바꿈
    /// </summary>
    /// <param name="transparent"> 인자로 들어온 재질</param>
    public void ChangeMaterial(Material transparent)
    {
        MeshRenderer missileRenderer = this.gameObject.GetComponent<MeshRenderer>();

        missileRenderer.material = transparent;
        missileMaterial = transparent;

        MissileTransparent = true;
    }

   /// <summary>
   /// 알파 값 변경 함수
   /// </summary>
   /// <param name="alphaValue"> 변경할 알파 값</param>
    public void ChangeAlpha(float alphaValue)
    {
        // 재질 연결을 아직 하지 않았으므로,
        if (missileMaterial == null)
            return;

        missileMaterial.color = new Color(missileMaterial.color.r, missileMaterial.color.g, missileMaterial.color.b, alphaValue);
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// Y축 미사일 충돌
    /// </summary>
    /// <param name="collision"> 충돌한 오브젝트 콜라이더 </param>
    protected override void OnCollisionEnter(Collision collision)
    {
        // 이미 충돌인지 확인
        if (this.CollisionOther)
        {
            return;  // 이미 처리됨
        }

        base.OnCollisionEnter(collision);

        // 폭발 이펙트를 정확한 충돌 위치에 표시하기 위해 접점 저장
        Vector3 contact = collision.contacts[0].point;

        ActiveBombEffect(contact);

        // 충돌체의 레이어에 따른 조치
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            this.CollisionOther = true;

            MissileSpawner.Instance.ReturnSpawner(MissileType.YAxis, this.gameObject);

            GameProgress.Instance.Score = GameProgress.Instance.Score;
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("Missile"))
        {
            // Y축 미사일과 충돌한다면, 두 미사일의 Y 값에 따라서 계속 떨어질지, 없어질지 결정 
            // 아래에 있는 미사일은 더 가속을 받고 떨어지며, 위에 있는 미사일은 풀로 돌아간다.
            // 혹시나 Y축이 순간적으로 가까운 경우가 있다면 인스턴스 ID를 통한 비교
            if (collision.gameObject.GetComponent<MissileYAxis>() != null)
            {
                float otherMissileY = collision.gameObject.transform.position.y;

                if (this.gameObject.transform.position.y > otherMissileY)
                {
                    // 미사일 속도 조정
                    Rigidbody lowerRigidBody = collision.gameObject.GetComponent<Rigidbody>();
                    lowerRigidBody.linearDamping = Mathf.Max(lowerRigidBody.linearDamping - 0.05f,1.5f);
                    this.gameObject.GetComponent<Missile>().CollisionOther = true;

                    // 이 미사일은 풀로 반환
                    MissileSpawner.Instance.ReturnSpawner(MissileType.YAxis, this.gameObject);
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
