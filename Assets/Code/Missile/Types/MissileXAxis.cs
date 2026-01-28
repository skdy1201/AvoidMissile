using System.Collections;
using UnityEngine;

public enum EnumXAxisMissile
{
    custom,
    real
}

/// <summary>
/// X축으로 움직이는 미사일
/// </summary>
/// <remarks>
/// 현실의 유도 미사일, 이동 - 회전을 나눠 움직이는 미사일의 종류로 나뉨
/// </remarks>
public class MissileXAxis : Missile
{
    #region Serialized Fields

    [Header("Stat")]
    [SerializeField] private int hp;
    [SerializeField] private float moveTime = 0f;
    [SerializeField] private float maxMoveTime;
    [SerializeField] private float rotateTime;
    [SerializeField] private float rotateSpeed;
    [SerializeField] private EnumXAxisMissile xAxisType;

    [Header("State")]
    [SerializeField] private bool movementActive = true;
    [SerializeField] private bool rotationActive = false;

    #endregion

    #region Private/Protected Fields

    private Vector2 playerPoint;

    #endregion

    #region Properties
    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        // Rigidbody 설정: 중력 비활성화, 저항 제거, XZ 평면 이동만 허용
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.constraints = RigidbodyConstraints.FreezePositionY
                                         | RigidbodyConstraints.FreezeRotationX
                                         | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    /// <summary>
    /// X축 미사일 타입을 랜덤 배정,
    /// 미사일의 랜덤 설정
    /// </summary>
    void Start()
    {
        xAxisType = (EnumXAxisMissile)Random.Range((int)EnumXAxisMissile.custom, (int)EnumXAxisMissile.real + 1);
        MissileSpawner.Instance.GetRandomSettingXAxis(this);
    }

    private void FixedUpdate()
    {

        // 커스텀 타입이라면, 이동과 회전을 분리
        // 현실 타입이라면, 매번 방향을 구하며 이동
        switch (xAxisType)
        {
            case EnumXAxisMissile.custom:
                if (moveTime > 0 && movementActive == true)
                {
                    moveTime -= Time.fixedDeltaTime;

                    // 이동 방향 업데이트 및 속도 적용
                    physics.direction = transform.forward;
                    ApplyVelocity();
                }
                else
                {
                    // 회전 중에는 속도 0
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                    }

                    movementActive = false;
                    moveTime = maxMoveTime;

                    playerPoint = new Vector2(GlobalData.Instance.Player.transform.position.x, GlobalData.Instance.Player.transform.position.z);

                    if (!rotationActive)
                    {
                        StartCoroutine(RotateToPlayer());
                        rotationActive = true;
                    }
                }
                break;
            case EnumXAxisMissile.real:
                // 이동 방향 업데이트 및 속도 적용
                physics.direction = transform.forward;
                ApplyVelocity();

                Vector3 targetDir = (GlobalData.Instance.Player.transform.position - transform.position).normalized;
                Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);

                // Y축 회전만 사용 (X, Z축 회전 무시)
                targetRot.z = targetRot.x = 0;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.fixedDeltaTime);
                break;

        }
    }
    #endregion

    #region Public Methods

    /// <summary>
    /// X축 미사일 이동 속도 초기화
    /// </summary>
    /// <param name="speed">이동 속도 (units/second)</param>
    public override void Initialize(float speed)
    {
        SetSpeed(speed, transform.forward);
        moveTime = maxMoveTime;
    }

    public void SetHP(int HP) => hp = HP;
    public void SetMoveTime(float moveTime) => maxMoveTime = moveTime;
    public void SetMoveSpeed(float moveSpeed)
    {
        physics.speed = moveSpeed;
    }
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
    /// Y축 미사일과 맞으면  HP 감소. 0이 되면 소멸.
    /// 충돌 지점에 폭발 효과
    /// </summary>
    /// <param name="collision"> 충돌 물체 </param>
    /// <remarks>
    /// 미사일의 요소만 관리
    /// 플레이어의 체력 감소는 플레이어가 담당
    /// </remarks>
    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        if (collision.gameObject.layer == LayerMask.NameToLayer("Missile") && collision.gameObject.GetComponent<MissileYAxis>() != null)
        {
            hp--;

            // 체력이 0이 되면 파괴
            if (hp <= 0)
            {
                MissileSpawner.Instance.SubSpawn(gameObject);
                Destroy(gameObject);
            }

            // 충돌 지점에 이펙트 스폰
            Vector3 contact = collision.contacts[0].point;

            ActiveBombEffect(contact);
        }
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// Custom 미사일 회전
    /// </summary>
    /// <remarks>
    /// 회전시간 동안에만 미사일이 회전
    /// </remarks>
    IEnumerator RotateToPlayer()
    {
        float currentRotateTimer = 0f;

        Vector2 currentPosition = new Vector2(gameObject.transform.position.x, gameObject.transform.position.z);
        Vector2 direction = (playerPoint - currentPosition).normalized;
        Vector2 currentFront = new Vector2(gameObject.transform.forward.x, gameObject.transform.forward.z);

        // 내적으로 각도 구하기
        float dot = Vector2.Dot(direction, currentFront);
        float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

        // 외적으로 회전 방향 구하기
        float cross = currentFront.x * direction.y - currentFront.y * direction.x;
        float rotationDirection = -Mathf.Sign(cross);

        // 회전 타이머까지 도달하지 않았다면 코루틴 내 반복
        while (currentRotateTimer <= rotateTime)
        {
            // 각도를 회전 시간으로 나누면, 1초에 회전할 각도가 나오고 deltaTime을 곱해 한 프레임 회전 각도
            float rotationThisFrame = (angle / rotateTime) * Time.deltaTime;
            
            // 회전각도와 방향을 곱해서 해당 방향으로 각도만큼 회전
            transform.Rotate(0, rotationThisFrame * rotationDirection, 0);
            
            currentRotateTimer += Time.deltaTime;
            
            yield return null;
        }

        // 이동 플래그 활성화 && 회전 플래그 비활성화
        movementActive = true;
        rotationActive = false;
    }

    #endregion

}
