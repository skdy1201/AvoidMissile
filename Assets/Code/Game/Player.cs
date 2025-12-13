using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Collections;
using System.Linq.Expressions;


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

    // �̵�, �����̵� ��ȯ ����
    private bool move;
    private bool slide = false;

    private bool reversemove = false;

    private bool powerJump = false;

    #endregion

    #region Properties
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

        // 2. Move Position 
        moveVector = new Vector3(x, 0, z) * moveSpeed * Time.fixedDeltaTime;

        move = true;

        // 슬라이드 체크
        if (slide)
        {
            move = false;

            moveVector = gameObject.transform.forward * (moveSpeed + 2.5f) * Time.fixedDeltaTime;
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
        if (slide)
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

    public void ActivateSlide() => slide = true;
    public void DeactivateSlide() => slide = false;

    public void PlayerEffectSound(int effectEnum)
    {
        AudioController.Instance.PlayPlayerEffect(effectEnum);
    }

    /// <summary>
    /// �ܺο��� �÷��̾��� �ӵ��� �����ϵ��� �ϱ� ���� public �Լ�
    /// </summary>
    /// <remarks>
    /// ���ο����� �ڷ�ƾ�� ���� ��ȭ
    /// </remarks>
    /// <param name="value"> �����ų �� </param>
    /// <param name="time"> ���� �ð� </param>
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
    /// �̻��� ���̾�� �浹 ��, ��� �̺�Ʈ ����
    /// </summary>
    /// <param name="collision"> �浹�� ������Ʈ�� Collision </param>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Missile"))
        {
            OnPlayerDead?.Invoke();
        }
    }

    #endregion

    #region Event Handlers

    // �÷��̾� ��� �̺�Ʈ
    static public UnityEvent OnPlayerDead = new UnityEvent();

    #endregion

    #region Coroutine

    /// <summary>
    /// �������� �԰� �� ��, Player�� �ӵ��� �����ϴ� �ڷ�ƾ
    /// </summary>
    /// <param name="speed"> �������� ���� �ӵ� ��ȭ�� </param>
    /// <param name="time"> ��ȭ �ð�</param>
    /// <returns></returns>
    IEnumerator ActiveSpeedChange(float speed, float time)
    {
        float origin = moveSpeed;

        moveSpeed += speed;

        // �ּ� �ӷ�
        if (moveSpeed <= 0)
            moveSpeed = 0.1f;

        yield return new WaitForSecondsRealtime(time);

        moveSpeed = origin;
    }

    IEnumerator ReverseCoroutine(float time)
    {
        reversemove = true;

        yield return new WaitForSecondsRealtime(time);

        reversemove = false;
    }

    IEnumerator JumpCoroutine(float time)
    {
        powerJump = true;

        yield return new WaitForSecondsRealtime(time);

        powerJump = false;
    }

    #endregion
}
