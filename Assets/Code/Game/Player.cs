using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Collections;
using UnityEngine.Rendering;


/// <summary>
/// ?뚮젅?댁뼱??議곗옉怨?愿?⑦븳 ?ㅽ겕由쏀듃
/// </summary>
public class Player : MonoBehaviour
{

    #region Serialized Fields

    [SerializeField] private VariableJoystick joystick;

    [FormerlySerializedAs("Speed")]
    [SerializeField] private float moveSpeed;

    // ?붾쾭洹몄슜 遺덉궗 蹂??
    [SerializeField] bool undeadPlayer;

    #endregion

    #region Private/Protected Fields

    // ?뚮젅?댁뼱 臾쇰━ 愿??蹂??
    private Rigidbody rigidBody;
    private Vector3 moveVector;

    private Animator playerAnimator;

    // ?뚮젅?댁뼱 議곗옉 愿??蹂??
    private bool move;
    private bool slide = false;
    private bool reversemove = false;
    private bool powerJump = false;
    private float bonusSpeed = 0f;

    /// <summary>
    /// 踰꾪봽媛 ?앸궗?뚯뿉??powerJump??寃쎌슦媛 ?덇린 ?뚮Ц?? ?대? 遺꾧컙?섍린 ?꾪븳 蹂??
    /// </summary>
    private bool activePowerJump = false;

    // ?좉툑 ?곹깭 泥댄겕 蹂??
    private bool lockSkill = false;

    // Player 遺??蹂??
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
    /// ?뚮젅?댁뼱??議곗옉???곕씪 蹂?붾? 以섏빞???붿냼?ㅼ쓣 誘몃━ 罹먯떛
    /// GlobalData???뚮젅?댁뼱 ?깅줉
    /// </summary>

    void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();

        GlobalData.Instance.Player = this.gameObject;
    }

    /// <summary>
    /// ??덉씠?댁쓽 ?대룞???대떦
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

 //?먮뵒?곗슜 ?ㅻ낫??媛꾨떒 議곗옉
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

        // ?띾룄???꾩씠?쒖쑝濡?蹂寃쎈맂 ?띾룄? 湲곕낯 ?띾룄瑜??뷀븳 媛믪쑝濡??곸슜
        float nowSpeed = moveSpeed + bonusSpeed;

        // 理쒖냼?띾룄 蹂댁옣
        if (nowSpeed <= 0f)
            nowSpeed = 0.1f;

        // 2. Move Position 
        moveVector = new Vector3(x, 0, z) * nowSpeed * Time.fixedDeltaTime;

        move = true;

        // ?щ씪?대뱶 泥댄겕
        if (slide)
        {
            move = false;

            moveVector = gameObject.transform.forward * (nowSpeed + 2.5f) * Time.fixedDeltaTime;
        }


        // ?대룞 寃곌낵 湲곕줉
        rigidBody.MovePosition(rigidBody.position + moveVector);

        //?대룞???녿떎硫?return
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
        // 議곗옉???곕Ⅸ 寃곌낵瑜?湲곕줉?대몦 蹂?섎? ?먮땲硫붿씠?곗? ?숆린??
        playerAnimator.SetBool("IsMove", move);
        playerAnimator.SetBool("IsSlide", slide);
        playerAnimator.SetBool("PowerJump", powerJump);

        // Player 遺덉궗 泥댄겕
        if (undeadPlayer)
            this.gameObject.GetComponent<Collider>().enabled = false;
        else
            this.gameObject.GetComponent<Collider>().enabled = true;

    }

    #endregion

    #region Public Methods

    // 踰꾪봽 ?곹깭?쇰븧, PowerJump??媛숈씠 active
    public void ActivateSlide()
    {
        // 媛뺥솕 ?곹깭?쇰븧, 異⑸룎?댁꽌 誘몄궗?쇱쓣 ?놁븿 ???덇린 ?뚮Ц?? ?쇰컲 ?щ씪?대뱶? ?ㅻⅨ 泥섎━
        if (powerJump)
        {
            activePowerJump = true;
            slide = true;
            return;
        }

        slide = true;
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), true);
    }

    // ?щ씪?대뱶 醫낅즺瑜?媛깆떊?섎뒗 ?⑥닔
    public void DeactivateSlide()
    {
        if(powerJump)
            activePowerJump = false;

        slide = false;
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), false);
    }

    /// <summary>
    /// ?먮땲硫붿씠?곗뿉???대깽?몃줈 ?묐룞?쒗궗 ?⑥닔
    /// </summary>
    /// <param name="effectEnum"></param>
    public void PlayerEffectSound(int effectEnum)
    {
        AudioController.Instance.PlayPlayerEffect(effectEnum);
    }

    /// <summary>
    /// ?몃??먯꽌 ?뚮젅?댁뼱???띾룄瑜?諛붽씀?꾨줉 ?붿껌?섎뒗 ?⑥닔
    /// </summary>
    /// <remarks>
    /// ?대??곸쑝濡?肄붾（?댁쓣 ?ъ슜
    /// </remarks>
    /// <param name="value"> 諛붽? ?띾룄 </param>
    /// <param name="time"> ?쒓컙 </param>
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
    /// ?ㅽ궗 ?좉툑? 以묒꺽?쇰줈 ?묒슜?쒗궎吏 ?딄린 ?뚮Ц?? ?대? ?좉툑 ?곹깭?쇰㈃ ?④낵 諛쒕룞??臾댄슚??
    /// </summary>
    /// <param name="time"></param>
    public void SkillLock(float time)
    {
        if (lockSkill)
            return;
        
        StartCoroutine(SlideLock(time));
    }

    /// <summary>
    /// 泥섏쓬 ?ㅽ룿?꾩튂濡??뚮젅?댁뼱 ?대룞, 遺???④낵媛 吏?띾릺???숈븞 臾댁쟻 泥섎━
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

    public void OffRevive()
    {
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Missile"), false);
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// ?뚮젅?댁뼱??異⑸룎 愿由?
    /// </summary>
    /// <remarks>
    /// PowerJump?쇰븧 異⑸룎???댁빞?섍린 ?뚮Ц??議곌굔 異붽?
    /// ?숈궗??遺?쒖씠 媛?ν븯湲??뚮Ц?? Layer 泥댄겕 異붽?
    /// </remarks>
    /// <param name="collision"> 異⑸룎??臾쇱껜??Collision </param>
    private void OnCollisionEnter(Collision collision)
    {

        if (collision.gameObject.layer == LayerMask.NameToLayer("GameBoundary"))
        {
            if (readyRevive == false)
                OnPlayerDead?.Invoke();
        }
        else if(collision.gameObject.layer == LayerMask.NameToLayer("Missile") && activePowerJump == false)
        {
            if (readyRevive == false)
                OnPlayerDead?.Invoke();
            else
            {
                // 遺???숈옉 諛??④낵 諛쒕룞
                ActiveRevive();
            }
        }
    }

    #endregion

    #region Event Handlers

    // ?뚮젅?댁뼱 ?щ쭩 ?대깽??
    public static UnityEvent OnPlayerDead = new UnityEvent();

    // 踰꾪듉???대깽???깅줉
    public static UnityEvent OnSkilllockOn = new UnityEvent();
    public static UnityEvent OnSkillLockOff = new UnityEvent();

    // 遺?쒖떆 ?댄럺???숈옉 ?대깽??
    public static UnityEvent ActiveEffect = new UnityEvent();

    #endregion

    #region Coroutine

    /// <summary>
    /// ?뚮젅?댁뼱???띾룄瑜?諛붽씀??肄붾（??
    /// </summary>
    /// <param name="speed"> 諛붽? ?띾룄 </param>
    /// <param name="time">  ?쒓컙 </param>
    /// <returns></returns>
    IEnumerator ActiveSpeedChange(float speed, float time)
    {
        bonusSpeed += speed;

        yield return new WaitForSeconds(time);

        bonusSpeed -= speed;
    }

    IEnumerator ReverseCoroutine(float time)
    {
        reversemove = !reversemove;

        yield return new WaitForSeconds(time);

        reversemove = !reversemove;
    }

    IEnumerator JumpCoroutine(float time)
    {
      powerJump = true;

      yield return new WaitForSeconds(time);

      powerJump = false;

    }

    /// <summary>
    /// ?щ씪?대뱶 ??肄붾（??
    /// </summary>
    /// <param name="time"> 踰꾪듉?ㅼ쓽 ?대깽?몃뱾???숈옉?쒗궎硫? 吏?띿떆媛꾩씠 ?앸굹硫??ㅼ떆 ?곹깭瑜?珥덇린??</param>
    /// <returns></returns>
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
