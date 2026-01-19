using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Y異?誘몄궗???ㅽ겕由쏀듃
/// </summary>
public class MissileYAxis : Missile
{
    #region Serialized Fields

    [SerializeField] public Vector2 XZCoord = new Vector2();
    [SerializeField] private Material missileMaterial;

    [SerializeField] private GameObject warningDecal;
    [SerializeField] private DecalProjector decal;
    [SerializeField] private float platformY;

    #endregion

    #region Private/Protected Fields

    private float dropPoint;

    [SerializeField] private Vector3 tileScale = Vector3.zero;

    private const float decalYOffset = 1.1f;
    #endregion

    #region Properties

    public bool MissileTransparent { get; set; } = false;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 肄쒕씪?대뜑 而댄룷?뚰듃 珥덇린?? 異⑸룎 ?먯젙???꾪빐 Awake?먯꽌 罹먯떛
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

    }

    private void Start()
    {
        dropPoint = GlobalData.Instance.MissileDropPoint - decalYOffset;
        platformY = MissileSpawner.Instance.gamePlatform.transform.position.y;
        tileScale = new Vector3(GlobalData.Instance.TileXScale, 1f, GlobalData.Instance.TileZScale);
    }

    private void FixedUpdate()
    {
        // ?곗뭡 ?꾩튂: ?ㅽ룿?????XZ 醫뚰몴 + ????믪씠
        Vector3 decalPoint = new Vector3(
            XZCoord.x,
            platformY + decalYOffset,
            XZCoord.y
        );
        warningDecal.transform.position = decalPoint;

        // ?믪씠 湲곕컲 ?ㅼ???怨꾩궛 (0~100)
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
        this.CollisionOther = false;
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
    /// 誘몄궗?쇱쓽 癒명떚由ъ뼹 蹂寃?
    /// </summary>
    /// <param name="transparent">?щ챸 癒명떚由ъ뼹</param>
    public void ChangeMaterial(Material transparent)
    {
        MeshRenderer missileRenderer = this.gameObject.GetComponent<MeshRenderer>();

        missileRenderer.material = transparent;
        missileMaterial = transparent;

        MissileTransparent = true;
    }

    /// <summary>
    /// ?뚰뙆 媛?蹂寃??⑥닔
    /// </summary>
    /// <param name="alphaValue">蹂寃쏀븷 ?뚰뙆 媛?/param>
    public void ChangeAlpha(float alphaValue)
    {
        // ?꾩쭅 癒명떚由ъ뼹???ㅼ젙?섏? ?딆븯?쇰?濡?
        if (missileMaterial == null)
            return;

        missileMaterial.color = new Color(missileMaterial.color.r, missileMaterial.color.g, missileMaterial.color.b, alphaValue);
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// Y異?誘몄궗??異⑸룎
    /// </summary>
    /// <param name="collision">異⑸룎???ㅻ툕?앺듃 肄쒕씪?대뜑</param>
    protected override void OnCollisionEnter(Collision collision)
    {
        // ?대? 異⑸룎?덈뒗吏 ?뺤씤
        if (this.CollisionOther)
        {
            return;  // ?대? 泥섎━??
        }

        base.OnCollisionEnter(collision);

        // ??컻 ?댄럺?몃? ?뺥솗??異⑸룎 ?꾩튂???쒖떆?섍린 ?꾪빐 醫뚰몴 ???
        Vector3 contact = collision.contacts[0].point;

        ActiveBombEffect(contact);

        // 異⑸룎泥댁쓽 ?덉씠?댁뿉 ?곕씪 遺꾧린
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            this.CollisionOther = true;

            MissileSpawner.Instance.ReturnSpawner(MissileType.YAxis, this.gameObject);

            GameProgress.Instance.Score = GameProgress.Instance.Score;
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("Missile"))
        {
            // Y異?誘몄궗?쇨낵 異⑸룎?쒕떎硫? ??誘몄궗?쇱쓽 Y 媛믪쓣 鍮꾧탳???꾩뿉 ?덉쑝硫????諛섑솚
            // ??? ?믪씠??誘몄궗?쇱씠?쇰㈃ ??鍮⑤━ ?⑥뼱吏?
            if (collision.gameObject.GetComponent<MissileYAxis>() != null)
            {
                float otherMissileY = collision.gameObject.transform.position.y;

                if (this.gameObject.transform.position.y > otherMissileY)
                {
                    Rigidbody lowerRigidBody = collision.gameObject.GetComponent<Rigidbody>();

                    // XZ ?띾룄 ?쒓굅 (?섏쭅 ?숉븯 ?좎?)
                    lowerRigidBody.linearVelocity = new Vector3(0, lowerRigidBody.linearVelocity.y, 0);

                    // ?숉븯 ?띾룄 利앷?
                    lowerRigidBody.linearDamping = Mathf.Max(lowerRigidBody.linearDamping - 0.05f, 1.5f);

                    this.gameObject.GetComponent<Missile>().CollisionOther = true;

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
