using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼들이 사용할 함수들을 모아놓은 싱글톤 오브젝트
/// </summary>
public class ButtonFunction : Singleton<ButtonFunction>
{
    #region Serialized Fields

    [SerializeField] private ButtonCooltime SlideCooltime;

    #endregion

    #region Private/Protected Fields
    #endregion

    #region Properties
    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Singleton의 Awake, 플레이어 사망 이벤트 등록
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        Player.OnPlayerDead.AddListener(() => OnPlayerDeath());
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// UI 상태를 갱신하고, UI 컨트롤러에게 UI 현재 활성화 UI를 갱신
    /// </summary>
    public void SettingGame()
    {
        UIController.Instance.DisableUIState((int)UIStateEnum.Main);
        UIController.Instance.EnableUIState((int)UIStateEnum.Option);

        UIController.Instance.UpdateUIStates(UIController.Instance.GetCurrentUIState());

    }

    /// <summary>
    /// 설정을 닫는 함수
    /// </summary>
    /// <remarks>
    /// UI 상태를 갱신하고, 타임 스케일을 1로 돌려놓음 
    /// </remarks>
    public void CloseSetting()
    {
        Time.timeScale = 1f;

        UIController.Instance.DisableUIState((int)UIStateEnum.Option);

        UIController.Instance.EnableUIState((int)UIStateEnum.Main);
        UIController.Instance.UpdateUIStates(UIController.Instance.GetCurrentUIState());

    }

    /// <summary>
    /// 옵션 변경 함수
    /// </summary>
    /// <remarks>
    /// 변경한 옵션을 설정하고, 닫는 함수
    /// </remarks>
    public void AcceptOption()
    {
        GameData.Instance.SetOption();

        CloseSetting();

    }

    /// <summary>
    /// 게임 종료 함수
    /// </summary>
    /// <remarks>
    /// 에디터에서 실행 할 땐, play mode를 종료 하고,
    /// 실제 어플리케이션이라면 종료
    /// </remarks>
    public void ExitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // 유니티 에디터에서 실행 중일 때
        #else
            Application.Quit(); // 일반 빌드에서 실행 중일 때
        #endif
    }

    /// <summary>
    /// 플레이어 슬라이드 함수
    /// </summary>
    /// <remarks>
    /// 쿨타임 동안 버튼이 비활성화되어 연속 사용을 방지합니다.
    /// </remarks>
    public void ExecuteSlide()
    {
        GameObject player = GlobalData.Instance.Player;

        if (player != null)
        {
            player.GetComponent<Player>().ActivateSlide();

            StartCoroutine(ActiveCooltime(SlideCooltime.GetButtonObject, SlideCooltime.GetCooldownImage, SlideCooltime.GetCooldown));
        }
    }

    public void SetButtonCooltime(ButtonCooltime buttonCooltime) => SlideCooltime = buttonCooltime;

    /// <summary>
    /// 리더보드 UI
    /// </summary>
    /// <remarks>
    /// UI 스테이트에 따라 자연스럽게 리더보드  UI가 활성화 된다.
    /// </remarks>
    public void LeaderBoard()
    {
        UIController.Instance.DisableUIState((int)UIStateEnum.Main);
        UIController.Instance.EnableUIState((int)UIStateEnum.LeaderBoard);

        UIController.Instance.UpdateUIStates(UIController.Instance.GetCurrentUIState());
    }

    /// <summary>
    /// 리더보드 UI를 닫는 함수
    /// </summary>
    public void CloseLeaderBoard()
    {
        UIController.Instance.DisableUIState((int)UIStateEnum.LeaderBoard);
        UIController.Instance.EnableUIState((int)UIStateEnum.Main);

        UIController.Instance.UpdateUIStates(UIController.Instance.GetCurrentUIState());
    }

    /// <summary>
    /// 테스트 레벨 용 함수 파티클 생성
    /// </summary>
    public void SpawnParticle()
    {
        GameObject gameObject = BoomEffectSpawner.Instance.RentSpawner(BoomParticle.Normal);
        gameObject.SetActive(true);
        gameObject.transform.position = Vector3.zero;

        foreach (var ps in gameObject.GetComponentsInChildren<ParticleSystem>(true))
        {
            // 안전하게 0프레임에서 시작
            ps.Simulate(0f, true, true);
            ps.Play(true);
        }
    }

    #endregion

    #region Private/Protected Methods

    protected override void StartProtocol()
    {
    }

    protected override void EndProtocol()
    {
    }

    /// <summary>
    /// 플레이어 사망시 동작시킬 함수.
    /// </summary>
    private void OnPlayerDeath()
    {
        //if Slider cooldown active, need stop coroutine
        StopAllCoroutines();
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// 쿨타임 동작 함수
    /// </summary>
    /// <param name="targetButton"> 슬라이딩 버튼 </param>
    /// <param name="cooldownBackground"> 슬라이딩 버튼 쿨타임 이미지 </param>
    /// <param name="cooldown"> 쿨타임 </param>
   IEnumerator ActiveCooltime(Button targetButton, Image cooldownBackground, float cooldown)
    {
        // 쿨타임 배경 활성화 및 슬라이딩 버튼 비활성화
        cooldownBackground.enabled = true;
        targetButton.enabled = false;

        float curtime = cooldown;

        // 코루틴 내 while 문으로 시간계산 및 이미지 변화
        while (curtime >= 0f)
        {

            if (Time.timeScale > 0f)
            {
                curtime -= Time.deltaTime;

                float ratio = Mathf.Clamp01(curtime / cooldown);

                cooldownBackground.fillAmount = ratio;
            }

            yield return null;

        }

        // 이미지 및 버튼 비활성화
        cooldownBackground.enabled = false;
        cooldownBackground.fillAmount = 1f;
        targetButton.enabled = true;
    }

    #endregion

}
