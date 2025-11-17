using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.Serialization;


/// <summary>
/// 플레이어 스크립트
/// </summary>
public class Player : MonoBehaviour
{

    #region Serialized Fields

    [SerializeField] private VariableJoystick joystick;

    [FormerlySerializedAs("Speed")]
    [SerializeField] private float moveSpeed;

    // 디버그용 플레이어 무적 변수
    [SerializeField] bool undeadPlayer;

    #endregion

    #region Private/Protected Fields

    private Rigidbody rigidBody;

    private Vector3 moveVector;

    private Animator playerAnimator;
    
    // 이동, 슬라이드 전환 변수
    private bool move;
    private bool slide = false;

    #endregion

    #region Properties
    #endregion

    #region Unity Lifecycle
    /// <summary>
    /// 플레이어 오브젝트의 다른 스크립트를 미리 매칭
    /// GlobalData에 플레이어 등록
    /// </summary>

    void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();

        GlobalData.Instance.Player = this.gameObject;
    }


    /// <summary>
    /// 
    /// </summary>
    void Update()
    {
        // 이동, 슬라이드 설정
        playerAnimator.SetBool("IsMove", move);
        playerAnimator.SetBool("IsSlide", slide);


        // 슬라이드일땐, Player 무적 처리
        if (slide)
        {
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), true);
        }
        else
        {
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), false);
        }

        // Player 무적 처리
        if (undeadPlayer)
            this.gameObject.GetComponent<Collider>().enabled = false;
        else
            this.gameObject.GetComponent<Collider>().enabled = true;

    }

    void FixedUpdate()
    {
        // 1. Input Value
        float x = joystick.Horizontal;
        float z = joystick.Vertical;

        // 2. Move Position 
        moveVector = new Vector3(x, 0, z) * moveSpeed * Time.fixedDeltaTime;

        move = true;

        // 슬라이드라면, 이동속도 증가
        if (slide)
        {
            move = false;
            moveVector = gameObject.transform.forward * (moveSpeed + 2.5f) * Time.fixedDeltaTime;
        }

        // 좌표 이동
        rigidBody.MovePosition(rigidBody.position + moveVector);

        // 입력이 없는 경우
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

    #endregion

    #region Public Methods

    public void ActivateSlide() => slide = true;
    public void DeactivateSlide() => slide = false;

    public void PlayerEffectSound(int effectEnum)
    {
        AudioController.Instance.PlayPlayerEffect(effectEnum);
    }
    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 미사일 레이어와 충돌 시, 사망 이벤트 동작
    /// </summary>
    /// <param name="collision"> 충돌한 오브젝트의 Collision </param>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Missile"))
        {
            OnPlayerDead?.Invoke();
        }
    }

    #endregion

    #region Event Handlers

    // 플레이어 사망 이벤트
    static public UnityEvent OnPlayerDead = new UnityEvent();

    #endregion

}
