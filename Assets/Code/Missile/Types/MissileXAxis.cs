using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Header("Missile Stat")]

    [FormerlySerializedAs("MissileHP")]
    [SerializeField] private int missileHP;

    [FormerlySerializedAs("MissileCurMoveTime")]
    [SerializeField] private float missileMoveTime = 0f;


    [FormerlySerializedAs("MissileMoveTime")]
    [SerializeField] private float missileMaxMoveTime;

    [FormerlySerializedAs("MissileMoveSpeed")]
    [SerializeField] private float missileMoveSpeed;

    [FormerlySerializedAs("MissileRotateTime")]
    [SerializeField] private float missileRotateTime;

    [FormerlySerializedAs("MissileRotateSpeed")]
    [SerializeField] private float missileRotateSpeed;

    [SerializeField] EnumXAxisMissile xAxisType;

    [Header("MissileState")]
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

    /// <summary>
    /// 미사일 이동하거나 회전
    /// </summary>
    void Update()
    {

        Debug.DrawRay(this.gameObject.transform.position, this.transform.forward, Color.yellow);

        Vector3 movevalue = this.transform.forward * Time.deltaTime * missileMoveSpeed;

        // 커스텀 타입이라면, 이동과 회전을 분리
        // 현실 타입이라면, 매번 방향을 구하며 이동
        switch (xAxisType)
        {
            case EnumXAxisMissile.custom:
                if (missileMoveTime > 0 && movementActive == true)
                {
                    missileMoveTime -= Time.deltaTime;

                    this.transform.position += movevalue;

                }
                else
                {
                    movementActive = false;
                    missileMoveTime = missileMaxMoveTime;

                    playerPoint = new Vector2(GlobalData.Instance.Player.transform.position.x, GlobalData.Instance.Player.transform.position.z);

                    if (!rotationActive)
                    {
                        StartCoroutine(RotateToPlayer());
                        rotationActive = true;
                    }
                }
                break;
            case EnumXAxisMissile.real:
                this.transform.position += movevalue;

                Vector3 targetDir = (GlobalData.Instance.Player.transform.position - transform.position).normalized;
                Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);

                // Y축 회전만 사용 (X, Z축 회전 무시)
                targetRot.z = targetRot.x = 0;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, missileRotateSpeed * Time.deltaTime);
                break;

        }
    }

    #endregion

    #region Public Methods

    public void SetHP(int HP) => missileHP = HP;
    public void SetMoveTime(float moveTime) => missileMaxMoveTime = moveTime;
    public void SetMoveSpeed(float moveSpeed) => missileMoveSpeed = moveSpeed;
    public void SetRotateTime(float rotateTime) => missileRotateTime = rotateTime;
    public void SetRotateSpeed(float rotateSpeed) => missileRotateSpeed = rotateSpeed;

    /// <summary>
    /// 미사일 스탯 설정
    /// </summary>
    /// <param name="hp"> 미사일 체력 </param>
    /// <param name="moveTime"> 이동 시간 </param>
    /// <param name="moveSpeed"> 이동 속도 </param>
    /// <param name="rotateTime"> 회전 시간 </param>
    /// <param name="rotateSpeed"> 회전 속도 </param>
    public void SetMissileStat(int hp, float moveTime, float moveSpeed, float rotateTime, float rotateSpeed)
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
            missileHP--;

            MissileSpawner.Instance.ReserveReturn(collision.gameObject);

            // 체력이 0이 되면 파괴
            if (missileHP <= 0)
            {
                MissileSpawner.Instance.SubSpawn(this.gameObject);
                Destroy(this.gameObject);
            }


            // 충돌 지점에 이펙트 스폰
            Vector3 contact = collision.contacts[0].point;

            GameObject gameObject = BoomEffectSpawner.Instance.RentSpawner(BoomParticle.Normal);

            gameObject.transform.position = contact;
            gameObject.SetActive(true);

            // 폭발 소리 재생
            AudioController.Instance.PlayExploreSound();
        }
    }

    /// <summary>
    /// Real 미사일 회전 각도 계산
    /// </summary>
    /// <returns> 플레이어 방향으로 회전할 각도 (부호 포함) </returns>
    private float GetAngletoPlayer()
    {
        // 현재 플레이어 위치 받기
        playerPoint = new Vector2(GlobalData.Instance.Player.transform.position.x, GlobalData.Instance.Player.transform.position.z);

        // 필요한 정보 매칭
        Vector2 currentPosition = new Vector2(this.gameObject.transform.position.x, this.gameObject.transform.position.z);
        Vector2 direction = (playerPoint - currentPosition).normalized;
        Vector2 currentFront = new Vector2(this.gameObject.transform.forward.x, this.gameObject.transform.forward.z);

        // 플레이어와의 각도 계산
        float dot = Vector2.Dot(direction, currentFront);
        float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

        // 회전 방향을 결정할 외적
        float cross = currentFront.x * direction.y - currentFront.y * direction.x;
        float rotationDirection = -Mathf.Sign(cross);

        // 방향과 각도를 곱해서 전방 기준으로 회전할 값을 리턴
        return angle * rotationDirection;
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

        Vector2 currentPosition = new Vector2(this.gameObject.transform.position.x, this.gameObject.transform.position.z);
        Vector2 direction = (playerPoint - currentPosition).normalized;
        Vector2 currentFront = new Vector2(this.gameObject.transform.forward.x, this.gameObject.transform.forward.z);

        // 내적으로 각도 구하기
        float dot = Vector2.Dot(direction, currentFront);
        float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

        // 외적으로 회전 방향 구하기
        float cross = currentFront.x * direction.y - currentFront.y * direction.x;
        float rotationDirection = -Mathf.Sign(cross);

        // 회전 타이머까지 도달하지 않았다면 코루틴 내 반복
        while (currentRotateTimer <= missileRotateTime)
        {
            // 각도를 회전 시간으로 나누면, 1초에 회전할 각도가 나오고 deltaTime을 곱해 한 프레임 회전 각도
            float rotationThisFrame = (angle / missileRotateTime) * Time.deltaTime;
            
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
