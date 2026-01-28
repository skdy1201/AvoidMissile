using UnityEngine;

/// <summary>
/// Missile들의 최상위 스크립트
/// </summary>
/// <remarks>
/// 미사일 콜라이더
/// </remarks>
public class Missile : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private int missileNumber;
    [SerializeField] private bool missileReturn;
    [SerializeField] private int spawnTime;

    #endregion

    #region Private/Protected Fields

    protected Collider missileCollider;
    protected Rigidbody missileRigidbody;
    [SerializeField] protected PhysicsData physics;

    #endregion

    #region Properties

    public int MissileNumber 
    { 
        get => missileNumber; 
        set => missileNumber = value;
    }

    public bool MissileReturn { get => missileReturn; set => missileReturn = value; }

    public int SpawnTime
    {
        get => spawnTime;
        set => spawnTime = value;
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 충돌 레이어 설정 및 컴포넌트 캐싱
    /// </summary>
    protected virtual void Awake()
    {
        this.gameObject.layer = LayerMask.NameToLayer("Missile");
        missileRigidbody = GetComponent<Rigidbody>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 미사일 초기화 (속도 설정)
    /// </summary>
    /// <param name="speed">속도 크기 (units/second)</param>
    public virtual void Initialize(float speed)
    {
        physics.speed = speed;
    }

    /// <summary>
    /// 미사일 속도와 방향을 설정
    /// </summary>
    /// <param name="speed">속도 크기 (units/second)</param>
    /// <param name="direction">이동 방향</param>
    public void SetSpeed(float speed, Vector3 direction)
    {
        physics.speed = speed;
        physics.direction = direction.normalized;
    }

    /// <summary>
    /// 현재 속도 설정을 Rigidbody에 적용
    /// </summary>
    protected void ApplyVelocity()
    {
        if (missileRigidbody != null)
        {
            missileRigidbody.linearVelocity = physics.Velocity;
        }
    }

    /// <summary>
    /// 충돌 지점의 폭발 효과를 동작시키기 위한 함수
    /// </summary>
    /// <param name="targetPosition"> 충돌한 지점 </param>
    public void ActiveBombEffect(Vector3 targetPosition)
    {
        // 폭발 이펙트 빌려와서 동작
        GameObject boomEffect = BoomEffectSpawner.Instance.RentSpawner(BoomParticle.Normal);

        boomEffect.transform.position = targetPosition;
        boomEffect.SetActive(true);

        AudioController.Instance.PlayExploreSound();
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// PowerJump 상태의 Player와 충돌했을때, 미사일은 파괴
    /// </summary>
    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            Player player = collision.gameObject.GetComponent<Player>();

            if(player.ActivePowerJump)
            {
                GameProgress.Instance.Score = GameProgress.Instance.Score;

                // 폭발 이펙트를 정확한 충돌 위치에 표시하기 위해 접점 저장
                Vector3 contact = collision.contacts[0].point;

                ActiveBombEffect(contact);

                this.gameObject.SetActive(false);

                if(this.gameObject.GetComponent<MissileYAxis>() != null)
                MissileSpawner.Instance.ReturnSpawner(MissileType.YAxis, this.gameObject);
                else
                    Destroy(this.gameObject);
            }
        }
    }

    #endregion

}
