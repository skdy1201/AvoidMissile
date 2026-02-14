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

    [SerializeField] private int number;
    [SerializeField] private bool returned;
    [SerializeField] private int spawnTime;

    #endregion

    #region Private/Protected Fields

    protected Collider col;
    protected Rigidbody rb;
    [SerializeField] protected PhysicsData physics;

    protected bool onDamage = true;

    #endregion

    #region Properties

    public int Number
    {
        get => number;
        set => number = value;
    }

    public bool Returned { get => returned; set => returned = value; }

    public int SpawnTime
    {
        get => spawnTime;
        set => spawnTime = value;
    }

    public bool Damage
    {
        get => onDamage;
        set => onDamage = value;
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 충돌 레이어 설정 및 컴포넌트 캐싱
    /// </summary>
    protected virtual void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Missile");
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Trigger 모드로 설정 (물리 충돌 반응 제거, OnTriggerEnter로 감지)
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Kinematic 모드로 설정 (Physics.Simulate 비용 제거, transform으로 직접 이동)
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 미사일 초기화 (내부 스탯으로 physics 설정)
    /// </summary>
    public virtual void Initialize()
    {
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
    /// 현재 속도 설정으로 이동 (transform 직접 조작)
    /// </summary>
    protected void ApplyVelocity()
    {
        transform.position += physics.Velocity * Time.fixedDeltaTime;
    }

    /// <summary>
    /// 충돌 지점의 폭발 효과를 동작시키기 위한 함수
    /// </summary>
    /// <param name="targetPosition"> 충돌한 지점 </param>
    /// <param name="type"> 폭발 파티클 타입 </param>
    public void ActiveBombEffect(Vector3 targetPosition, BoomParticle type = BoomParticle.Normal)
    {
        // 폭발 이펙트 빌려와서 동작
        GameObject boomEffect = BoomEffectSpawner.Instance.RentSpawner(type);

        boomEffect.transform.position = targetPosition;
        boomEffect.SetActive(true);

        if (type == BoomParticle.Grand)
            AudioController.Instance.PlayBoomSound(2);
        else
            AudioController.Instance.PlayExploreSound();
    }

    #endregion

}
