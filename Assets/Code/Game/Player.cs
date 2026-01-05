using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Collections;
using UnityEngine.Rendering;


/// <summary>
/// 플레이어의 조작과 관련한 스크립트
/// </summary>
public class Player : MonoBehaviour
{

    #region Serialized Fields

    [SerializeField] private VariableJoystick joystick;

    [FormerlySerializedAs("Speed")]
    [SerializeField] private float moveSpeed;

    // 디버그용 불사 변수
    [SerializeField] bool undeadPlayer;

    #endregion

    #region Private/Protected Fields

    // 플레이어 물리 관련 변수
    private Rigidbody rigidBody;
    private Vector3 moveVector;

    private Animator playerAnimator;

    // 플레이어 조작 관련 변수
    private bool move;
    private bool slide = false;
    private bool reversemove = false;
    private bool powerJump = false;
    private float bonusSpeed = 0f;

    /// <summary>
    /// 버프가 끝났음에도 powerJump인 경우가 있기 때문에, 이를 분간하기 위한 변수
    /// </summary>
    private bool activePowerJump = false;

    // 잠금 상태 체크 변수
    private bool lockSkill = false;

    // Player 부활 변수
    private bool readyRevive = false;

    #endregion

    #region Properties

    public bool ActivePowerJump
    {
        get { return activePowerJump; }
    }

    public bool PowerJump
    {
        get { return powerJump; }
    }

    public bool LockSkill
    {
        get { return lockSkill; }
    }

    public bool Revive
    {
        get { return readyRevive; }
        set { readyRevive = value;}
    }

    #endregion

    #region Unity Lifecycle
    /// <summary>
    /// 플레이어의 조작에 따라 변화를 줘야할 요소들을 미리 캐싱
    /// GlobalData에 플레이어 등록
    /// </summary>

    void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();

        GlobalData.Instance.Player = this.gameObject;
    }

    /// <summary>
    /// 풀레이어의 이동을 담당
    /// </summary>
    void FixedUpdate()
    {
        // 1. Input Value
        float x = joystick.Horizontal;
        float z = joystick.Vertical;

        if (reversemove)
        {
            x = -x;
            z = -z;
        }

 //에디터용 키보드 간단 조작
#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.W))
        {
            z = 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            z = -1f;
        }

        if (Input.GetKey(KeyCode.A))
        {
            x = -1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            x = 1f;
        }
#endif

        // 속도는 아이템으로 변경된 속도와 기본 속도를 더한 값으로 적용
        float nowSpeed = moveSpeed + bonusSpeed;

        // 최소속도 보장
        if (nowSpeed <= 0f)
            nowSpeed = 0.1f;

        // 2. Move Position 
        moveVector = new Vector3(x, 0, z) * nowSpeed * Time.fixedDeltaTime;

        move = true;

        // 슬라이드 체크
        if (slide)
        {
            move = false;

            moveVector = gameObject.transform.forward * (nowSpeed + 2.5f) * Time.fixedDeltaTime;
        }


        // 이동 결과 기록
        rigidBody.MovePosition(rigidBody.position + moveVector);

        //이동이 없다면 return
        if (moveVector.sqrMagnitude == 0)
        {
            move = false;
            return; // #. No input = No Rotation
        }

        // 3. Move Rotation
        Quaternion dirQuat = Quaternion.LookRotation(moveVector);
        Quaternion moveQuat = Quaternion.Slerp(rigidBody.rotation, dirQuat, 0.3f);
        rigidBody.MoveRotation(moveQuat);


    }

    /// <summary>
    /// 
    /// </summary>
    void Update()
    {
        // 조작에 따른 결과를 기록해둔 변수를 에니메이터와 동기화
        playerAnimator.SetBool("IsMove", move);
        playerAnimator.SetBool("IsSlide", slide);
        playerAnimator.SetBool("PowerJump", powerJump);


        // 슬라이딩 시 플레이어 무적
        if (slide && activePowerJump == false)
        {
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), true);
        }
        else
        {
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), false);
        }

        // Player 불사 체크
        if (undeadPlayer)
            this.gameObject.GetComponent<Collider>().enabled = false;
        else
            this.gameObject.GetComponent<Collider>().enabled = true;

    }

    #endregion

    #region Public Methods

    // 버프 상태일땐, PowerJump도 같이 active
    public void ActivateSlide()
    {
        if (powerJump)
            activePowerJump = true;

        slide = true;

    }

    // 슬라이드 종료를 갱신하는 함수
    public void DeactivateSlide()
    {
        if(powerJump)
            activePowerJump = false;

        slide = false;
    } 

    /// <summary>
    /// 에니메이터에서 이벤트로 작동시킬 함수
    /// </summary>
    /// <param name="effectEnum"></param>
    public void PlayerEffectSound(int effectEnum)
    {
        AudioController.Instance.PlayPlayerEffect(effectEnum);
    }

    /// <summary>
    /// 외부에서 플레이어의 속도를 바꾸도록 요청하는 함수
    /// </summary>
    /// <remarks>
    /// 내부적으로 코루틴을 사용
    /// </remarks>
    /// <param name="value"> 바꿀 속도 </param>
    /// <param name="time"> 시간 </param>
    public void ChangeSpeed(float value, float time)
    {
        StartCoroutine(ActiveSpeedChange(value, time));
    }
    
    public void ControlReverse(float time)
    {
        StartCoroutine(ReverseCoroutine(time));
    }

    public void ReinforceSlide(float time)
    {
        StartCoroutine(JumpCoroutine(time));
    }

    /// <summary>
    /// 스킬 잠금은 중첩으로 작용시키지 않기 때문에, 이미 잠금 상태라면 효과 발동을 무효화
    /// </summary>
    /// <param name="time"></param>
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
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), true);
        this.transform.position = Vector3.zero;
        PlayerEffectSound((int)PlayerEffectSFX.Respawn);
    }

    public void OffRevive()
    {
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), false);
        readyRevive = false;
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 플레이어의 충돌 관리
    /// </summary>
    /// <remarks>
    /// PowerJump일땐 충돌을 해야하기 때문에 조건 추가
    /// 낙사도 부활이 가능하기 때문에, Layer 체크 추가
    /// </remarks>
    /// <param name="collision"> 충돌한 물체의 Collision </param>
    private void OnCollisionEnter(Collision collision)
    {
        if ((collision.gameObject.layer == LayerMask.NameToLayer("GameBoundary") || collision.gameObject.layer == LayerMask.NameToLayer("Missile")) && activePowerJump == false) 
        {
            if(readyRevive == false)
                OnPlayerDead?.Invoke();
            else
            {
                // 부활 동작 및 효과 발동
                ActiveRevive();
                ActiveEffect.Invoke();

            }
        }
    }

    #endregion

    #region Event Handlers

    // 플레이어 사망 이벤트
    static public UnityEvent OnPlayerDead = new UnityEvent();

    // 버튼의 이벤트 등록
    static public UnityEvent OnSkilllockOn = new UnityEvent();
    static public UnityEvent OnSkillLockOff = new UnityEvent();

    // 부활시 이펙트 동작 이벤트
    static public UnityEvent ActiveEffect = new UnityEvent();

    #endregion

    #region Coroutine

    /// <summary>
    /// 플레이어의 속도를 바꾸는 코루틴
    /// </summary>
    /// <param name="speed"> 바꿀 속도 </param>
    /// <param name="time">  시간 </param>
    /// <returns></returns>
    IEnumerator ActiveSpeedChange(float speed, float time)
    {
        bonusSpeed += speed;

        yield return new WaitForSecondsRealtime(time);

        bonusSpeed -= speed;
    }

    IEnumerator ReverseCoroutine(float time)
    {
        reversemove = !reversemove;

        yield return new WaitForSecondsRealtime(time);

        reversemove = !reversemove;
    }

    IEnumerator JumpCoroutine(float time)
    {
      powerJump = true;

      yield return new WaitForSecondsRealtime(time);

      powerJump = false;

    }

    /// <summary>
    /// 슬라이드 락 코루틴
    /// </summary>
    /// <param name="time"> 버튼들의 이벤트들을 동작시키며, 지속시간이 끝나면 다시 상태를 초기화 </param>
    /// <returns></returns>
    IEnumerator SlideLock(float time)
    {
        lockSkill = true;
        OnSkilllockOn.Invoke();

        yield return new WaitForSecondsRealtime(time);

        lockSkill = false;

        OnSkillLockOff.Invoke();
    }

    #endregion
}
