using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 스킬 관련 함수와 연결되는 ButtonUI
/// </summary>
/// <remarks>
/// 스킬 잠금 이미지와, 쿨타임 표시도 같이 관리
/// </remarks>
public class SkillButton : ButtonUI
{
    #region Serialized Fields

    [Header("CoolTime Setting")]
    [SerializeField] private float cooldown = 3f;
    [SerializeField] private float remainCooldown = 0f;

    #endregion

    #region Private/Protected Fields

    private Image grayImage;

    private bool inCooldown = false;

    private GameObject lockUI;

    #endregion
    
    #region Properties
    public bool Cooldown
    {
        get { return inCooldown; }
        set { inCooldown = value; }
    }

    public float RemainCooldown
    {
        get { return remainCooldown; }
        set { remainCooldown = value; }
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 쿨타임 이미지는 비활성화
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        Init();
        grayImage.enabled = false;
    }
    /// <summary>
    /// 쿨타임 도중 사망해서 부활하면, 코루틴이 중단되었기 때문에, 다시 남은 쿨타임만큼 코루틴 시전
    /// </summary>
    private void OnEnable()
    {
        if (type == ButtonType.Slide && inCooldown == true && remainCooldown > 0f)
        {
            Debug.Log("Revive Cooldown");
            StartCoroutine(ActiveCooltime());

        }
    }

    #endregion

    #region Public Methods

    public override void Init()
    {
        base.Init();

        switch (type)
        {
            case ButtonType.Slide:
                unityButton.onClick.AddListener(() => ExecuteSlide());
                Player.OnSkillLockOff.AddListener(() => OffLock());
                Player.OnSkilllockOn.AddListener(() => ActiveLock());
                break;
        }
    }

    #endregion

    #region Private/Protected Methods

    public void ActiveLock()
    {
        lockUI.SetActive(true);
    }

    public void OffLock()
    {
        LockUI lockui = lockUI.GetComponent<LockUI>();
        lockui.OffLockUI();
    }

    public void RegisterLock(GameObject ui)
    {
        lockUI = ui;
    }

    /// <summary>
    /// 플레이어 슬라이드 함수
    /// </summary>
    /// <remarks>
    /// 쿨타임 동안 버튼이 비활성화되어 연속 사용을 방지합니다.
    /// 스킬 잠금 여부도 같이 체크
    /// </remarks>
    public void ExecuteSlide()
    {
        Player player = GlobalData.Instance.Player.GetComponent<Player>();

        if (player != null && player.LockSkill == false)
        {
            player.ActivateSlide();

            StartCoroutine(ActiveCooltime());
        }
    }

    #endregion

    #region Coroutine

    /// <summary>
    /// 쿨타임 동작 함수
    /// </summary>
    /// <param name="targetButton"> 슬라이딩 버튼 </param>
    /// <param name="cooldownBackground"> 슬라이딩 버튼 쿨타임 이미지 </param>
    /// <param name="cooldown"> 잔여 쿨타임 or 전체 쿨타임 </param>
    IEnumerator ActiveCooltime()
    {

        if (remainCooldown <= 0)
            remainCooldown = cooldown;

            // 쿨타임 배경 활성화 및 슬라이딩 버튼 비활성화
            grayImage.enabled = true;
        unityButton.enabled = false;

        inCooldown = true;

        // 코루틴 내 while 문으로 시간계산 및 이미지 변화
        while (remainCooldown >= 0f)
        {

            if (Time.timeScale > 0f)
            {
                remainCooldown -= Time.deltaTime;

                float ratio = Mathf.Clamp01(remainCooldown / cooldown);

                grayImage.fillAmount = ratio;
            }

            yield return new WaitForSecondsRealtime(Time.deltaTime);

        }

        inCooldown = false;

        // 이미지 및 버튼 비활성화
        grayImage.enabled = false;
        grayImage.fillAmount = 1f;
        unityButton.enabled = true;
    }

    #endregion
}