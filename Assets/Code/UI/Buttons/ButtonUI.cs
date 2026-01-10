using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 버튼 타입에 따라 적절한 기능을 자동으로 연결
/// </summary>
/// <remarks>
/// Awake에서 자동으로 ButtonType 열거형에 따른 onClick 이벤트를 등록
/// ButtonFunction 싱글톤을 통해 게임 전반의 버튼 동작을 처리
/// </remarks>
public class ButtonUI : BaseUI, InterfaceUI
{
    #region Serialized Fields

    /// <summary>
    /// 버튼 종류에 대한 열거형
    /// </summary>
    [SerializeField] private ButtonType type;
    #endregion

    #region Property

    public ButtonType buttonType => type;

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

    private GameObject lockUI;

    private bool inCooldown = false;
    private float remainCooldown = 0f;

    #region Unity Lifecycle

    /// <summary>
    /// 부모 클래스의 UI 등록 후 버튼 이벤트를 초기화합니다.
    /// </summary>
    /// <remarks>
    /// base.Awake()에서 UI 리스트 등록이 먼저 수행
    /// Init()은 반드시 base.Awake() 호출 이후에 실행.
    /// </remarks>
    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    private void OnEnable()
    {
        if (type == ButtonType.Revive && GameProgress.Instance.PlayerAlive)
            gameObject.SetActive(false);
        else if (type == ButtonType.Slide && inCooldown == true && remainCooldown > 0f)
            AdReviveinCooldown();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 버튼 타입에 따라 onClick 이벤트 리스너를 등록.
    /// </summary>
    /// <remarks>
    /// Button 컴포넌트가 없을 경우 아무 동작 x.
    /// 각 ButtonType에 대응하는 ButtonFunction 메서드를 자동으로 연결.
    /// </remarks>
    public void Init()
    {
        Button button = GetComponent<Button>();

        if (button != null)
        {
            switch (type)
            {
                case ButtonType.Retry:
                    button.onClick.AddListener(() => SceneController.Instance.RestartPlayScene(this.gameObject));
                    break;
                case ButtonType.RetryTest:
                    button.onClick.AddListener(() => SceneController.Instance.RestartTestScene());
                    break;
                case ButtonType.Title:
                case ButtonType.Play:
                    button.onClick.AddListener(() => SceneController.Instance.ChangeScene());
                    break;
                case ButtonType.Edit:
                    button.onClick.AddListener(() => SceneController.Instance.ChnageTestScnen());
                    break;
                case ButtonType.Exit:
                    button.onClick.AddListener(() => ButtonFunction.Instance.ExitGame());
                    break;
                case ButtonType.Option:
                    button.onClick.AddListener(() => ButtonFunction.Instance.SettingGame());
                    break;
                case ButtonType.OptionAccept:
                    button.onClick.AddListener(() => ButtonFunction.Instance.AcceptOption());
                    break;
                case ButtonType.OptionClose:
                    button.onClick.AddListener(() => ButtonFunction.Instance.CloseSetting());
                    break;
                case ButtonType.Slide:
                    button.onClick.AddListener(() => ButtonFunction.Instance.ExecuteSlide());
                    break;
                case ButtonType.LeaderBoard:
                    button.onClick.AddListener(() => ButtonFunction.Instance.LeaderBoard());
                    break;
                case ButtonType.LeaderBoardClose:
                    button.onClick.AddListener(() => ButtonFunction.Instance.CloseLeaderBoard());
                    break;
                case ButtonType.ParticleSpawn:
                    button.onClick.AddListener(() => ButtonFunction.Instance.SpawnParticle());
                    break;
                case ButtonType.Revive:
                    button.onClick.AddListener(() => ButtonFunction.Instance.ReviveAdvertise());
                    break;
            }
        }

        if(buttonType == ButtonType.Slide)
        {
            Player.OnSkillLockOff.AddListener(() => OffLock());
            Player.OnSkilllockOn.AddListener(() => ActiveLock());
        }

    }

    #endregion

    public void RegisterLock(GameObject ui)
    {
        lockUI = ui;
    }
    public void ActiveLock()
    {
        lockUI.SetActive(true);
    }

    public void OffLock()
    {
        LockUI lockui = lockUI.GetComponent<LockUI>();
        lockui.OffLockUI();
    }

    public void AdReviveinCooldown()
    {
        if (remainCooldown > 0 && inCooldown == true)
            ButtonFunction.Instance.ActiveRemainCooldown();
           
    }
}
