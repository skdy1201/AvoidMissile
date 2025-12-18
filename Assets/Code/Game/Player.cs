using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;


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

    private Rigidbody rigidBody;

    private Vector3 moveVector;

    private Animator playerAnimator;

    private bool move;

    private bool slide = false;

    private bool reversemove = false;

    private bool powerJump = false;

    /// <summary>
    /// 버프가 끝났음에도 powerJump인 경우가 있기 때문에, 이를 분간하기 위한 변수
    /// </summary>
    private bool activePowerJump = false;

    private float bonusSpeed = 0f;

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

    public void DeactivateSlide()
    {
        if(powerJump)
            activePowerJump = false;

        slide = false;
    } 

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

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 플레이어의 충돌 관리
    /// </summary>
    /// <remarks>
    /// PowerJump일땐 충돌을 해야하기 때문에 조건 추가
    /// </remarks>
    /// <param name="collision"> 충돌한 물체의 Collision </param>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Missile") && activePowerJump == false) 
        {
            OnPlayerDead?.Invoke();
        }
    }

    #endregion

    #region Event Handlers

    // 플레이어 사망 이벤트
    static public UnityEvent OnPlayerDead = new UnityEvent();

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

    #endregion
}
