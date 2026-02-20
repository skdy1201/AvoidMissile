using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Collections;

/// <summary>
/// 플레이어의 조작과 관련한 스크립트
/// </summary>
/// <remarks>
/// 이동/회전 로직은 BehaviorTree Leaf 노드에서 처리
/// 버프/디버프, 충돌, 부활 등 이벤트성 로직은 이 스크립트에서 처리
/// </remarks>
public class Player : MonoBehaviour
{

    #region Serialized Fields

    [SerializeField] private VariableJoystick joystick;

    [SerializeField] private float moveSpeed;

    // 디버그용 불사 변수
    [SerializeField] private bool undeadPlayer;

    #endregion

    #region Private/Protected Fields

    // 플레이어 물리 관련 변수
    private Rigidbody rigidBody;
    private Collider playerCollider;
    private Animator playerAnimator;

    // BT 실행 관련 변수
    private BehaviorTreeRunner runner;

    // 플레이어 조작 관련 변수
    private bool move;
    private Vector2 joystickInput = Vector2.zero;
    private bool slide = false;
    private bool reversemove = false;
    private bool powerJump = false;
    private float nowSpeed = 0f;
    private float bonusSpeed = 0f;

    /// <summary>
    /// 버프가 슬라이드에서 powerJump의 경우가 있기 때문에, 이를 구분하기 위한 변수
    /// </summary>
    private bool activePowerJump = false;

    // 스킬 잠금 상태 체크 변수
    private bool lockSkill = false;

    // Player 부활 변수
    private bool readyRevive = false;

    #endregion

    #region Properties

    /// <summary>
    /// BT Leaf 노드에서 사용하기 위해 캐싱된 Rigidbody 제공
    /// </summary>
    public Rigidbody Rigidbody => rigidBody;

    /// <summary>
    /// 조이스틱 입력값 (에디터에서는 키보드 입력 포함)
    /// </summary>
    public Vector2 MoveValue => joystickInput;

    /// <summary>
    /// 이동 상태 플래그 (애니메이션 동기화용)
    /// </summary>
    public bool Move
    {
        get => move;
        set => move = value;
    }

    /// <summary>
    /// 슬라이드 상태 플래그 (버튼에서 제어)
    /// </summary>
    public bool Slide
    {
        get => slide;
        set => slide = value;
    }

    /// <summary>
    /// 현재 적용 중인 이동 속도 (기본 속도 + 버프 속도)
    /// </summary>
    public float Speed => nowSpeed;

    public bool ActivePowerJump => activePowerJump;

    public bool PowerJump => powerJump;

    public bool LockSkill => lockSkill;

    public bool Revive
    {
        get => readyRevive;
        set => readyRevive = value;
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 플레이어의 조작에 따라 변하지 않아야 하는 요소들을 미리 캐싱
    /// GlobalData에 플레이어 등록
    /// </summary>
    void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        playerAnimator = GetComponent<Animator>();
        runner = GetComponent<BehaviorTreeRunner>();

        GlobalData.Instance.Player = this.gameObject;
    }

    /// <summary>
    /// 플레이어의 이동을 담당
    /// </summary>
    /// <remarks>
    /// 입력값 수집 후 BehaviorTree 실행
    /// 실제 이동/회전은 BT Leaf 노드에서 처리
    /// </remarks>
    void FixedUpdate()
    {
        // 입력값 수집
        float x = joystick.Horizontal;
        float z = joystick.Vertical;

        // 조작 반전 디버프 적용
        if (reversemove)
        {
            x = -x;
            z = -z;
        }

#if UNITY_EDITOR
        // 에디터용 키보드 간단 조작
        if (Input.GetKey(KeyCode.W)) z = 1f;
        if (Input.GetKey(KeyCode.S)) z = -1f;
        if (Input.GetKey(KeyCode.A)) x = -1f;
        if (Input.GetKey(KeyCode.D)) x = 1f;
#endif

        // BT에서 사용할 입력값 저장
        joystickInput = new Vector2(x, z);

        // 속도 계산 (기본 속도 + 버프 속도)
        nowSpeed = moveSpeed + bonusSpeed;

        // 최소속도 보장
        if (nowSpeed <= 0f)
            nowSpeed = 0.1f;

        // BT 실행 (이동/회전은 Leaf 노드에서 처리)
        runner.RunBT();
    }

    /// <summary>
    /// 애니메이션 업데이트
    /// </summary>
    void Update()
    {
        // 조작에 따른 결과를 기록한 변수에 애니메이션을 동기화
        playerAnimator.SetBool("IsMove", move);
        playerAnimator.SetBool("IsSlide", slide);
        playerAnimator.SetBool("PowerJump", powerJump);

        // Player 불사 체크
        playerCollider.enabled = !undeadPlayer;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 슬라이드 시작
    /// </summary>
    /// <remarks>
    /// PowerJump 상태일 때는 미사일과 충돌 가능 (미사일 파괴)
    /// 일반 슬라이드는 미사일과 충돌 무시
    /// </remarks>
    public void ActivateSlide()
    {
        // 강화 상태알림, 충돌해서 미사일을 없앨 수 있기 때문에, 일반 슬라이드와 다른 처리
        if (powerJump)
        {
            activePowerJump = true;
            slide = true;
            return;
        }

        slide = true;
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), true);
    }

    /// <summary>
    /// 슬라이드 종료
    /// </summary>
    public void DeactivateSlide()
    {
        if (powerJump)
            activePowerJump = false;

        slide = false;
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), false);
    }

    /// <summary>
    /// 애니메이션에서 이벤트로 구동하는 함수
    /// </summary>
    /// <param name="effectEnum"> 재생할 효과음 종류 </param>
    public void PlayerEffectSound(int effectEnum)
    {
        AudioController.Instance.PlayPlayerEffect(effectEnum);
    }

    /// <summary>
    /// 아이템에서 플레이어의 속도를 바꾸도록 요청하는 함수
    /// </summary>
    /// <remarks>
    /// 내부적으로 코루틴을 사용
    /// </remarks>
    /// <param name="value"> 바꿀 속도 </param>
    /// <param name="time"> 지속시간 </param>
    public void ChangeSpeed(float value, float time)
    {
        StartCoroutine(ActiveSpeedChange(value, time));
    }

    /// <summary>
    /// 조작 반전 디버프 적용
    /// </summary>
    /// <param name="time"> 지속시간 </param>
    public void ControlReverse(float time)
    {
        StartCoroutine(ReverseCoroutine(time));
    }

    /// <summary>
    /// 슬라이드 강화 버프 적용 (PowerJump)
    /// </summary>
    /// <remarks>
    /// PowerJump 상태에서는 슬라이드로 미사일 파괴 가능
    /// </remarks>
    /// <param name="time"> 지속시간 </param>
    public void ReinforceSlide(float time)
    {
        StartCoroutine(JumpCoroutine(time));
    }

    /// <summary>
    /// 미사일 스킬을 중첩으로 작용시키지 않기 때문에, 이미 스킬 상태이면 효과 발동이 무효화
    /// </summary>
    /// <param name="time"> 지속시간 </param>
    public void SkillLock(float time)
    {
        if (lockSkill)
            return;

        StartCoroutine(SlideLock(time));
    }

    /// <summary>
    /// 처음 스폰위치로 플레이어 이동, 부활 효과가 지속되는 동안 무적 처리
    /// </summary>
    public void ActiveRevive()
    {
        GameProgress.Instance.PlayerAlive = true;
        readyRevive = false;
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), true);
        this.transform.position = Vector3.zero;
        PlayerEffectSound((int)PlayerEffectSFX.Respawn);
        ActiveEffect.Invoke();
    }

    /// <summary>
    /// 부활 무적 효과 종료
    /// </summary>
    public void OffRevive()
    {
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), false);
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 플레이어의 충돌 관리
    /// </summary>
    /// <remarks>
    /// PowerJump일때 충돌을 피해야하기 때문에 조건 추가
    /// 부활이 가능하기 때문에 Layer 체크 추가
    /// </remarks>
    /// <param name="collision"> 충돌한 물체의 Collision </param>
    private void OnCollisionEnter(Collision collision)
    {
        int collisionLayer = collision.gameObject.layer;

        if (collisionLayer == LayerMask.NameToLayer("GameBoundary"))
        {
            if (readyRevive == false)
                OnPlayerDead?.Invoke();
        }
        else if (collisionLayer == LayerMask.NameToLayer("Missile") && activePowerJump == false)
        {
            if (readyRevive == false)
                OnPlayerDead?.Invoke();
            else
            {
                // 부활 동작 및 효과 발동
                ActiveRevive();
            }
        }
    }

    #endregion

    #region Event Handlers

    // 플레이어 사망 이벤트
    public static UnityEvent OnPlayerDead = new UnityEvent();

    // 버튼에 이벤트 등록
    public static UnityEvent OnSkilllockOn = new UnityEvent();
    public static UnityEvent OnSkillLockOff = new UnityEvent();

    // 부활시 이펙트 동작 이벤트
    public static UnityEvent ActiveEffect = new UnityEvent();

    #endregion

    #region Coroutines

    /// <summary>
    /// 플레이어의 속도를 바꾸는 코루틴
    /// </summary>
    /// <param name="speed"> 바꿀 속도 </param>
    /// <param name="time"> 지속시간 </param>
    IEnumerator ActiveSpeedChange(float speed, float time)
    {
        bonusSpeed += speed;

        yield return new WaitForSeconds(time);

        bonusSpeed -= speed;
    }

    /// <summary>
    /// 조작 반전 코루틴
    /// </summary>
    /// <param name="time"> 지속시간 </param>
    IEnumerator ReverseCoroutine(float time)
    {
        reversemove = !reversemove;

        yield return new WaitForSeconds(time);

        reversemove = !reversemove;
    }

    /// <summary>
    /// 슬라이드 강화 코루틴 (PowerJump)
    /// </summary>
    /// <param name="time"> 지속시간 </param>
    IEnumerator JumpCoroutine(float time)
    {
        powerJump = true;

        yield return new WaitForSeconds(time);

        powerJump = false;
    }

    /// <summary>
    /// 슬라이드 잠금 코루틴
    /// </summary>
    /// <param name="time"> 버튼들의 이벤트들을 동작시키며, 지속시간이 끝나면 다시 상태를 초기화</param>
    IEnumerator SlideLock(float time)
    {
        lockSkill = true;
        OnSkilllockOn.Invoke();

        yield return new WaitForSeconds(time);

        lockSkill = false;

        OnSkillLockOff.Invoke();
    }

    #endregion
}
