using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum ButtonType
{
    None,
    Retry,
    Title,
    Play,
    Exit,
    Option,
    OptionAccept,
    OptionClose,
    Slide,
    LeaderBoard,
    LeaderBoardClose,
    ParticleSpawn,
    RetryTest,
    Edit,
};

public enum UIStateEnum
{
    Main = 1,
    Option = 2,
    GameOver = 4,
    LeaderBoard = 8,
}

//TODO : DISABLE과 UPDATE UISTATE가 좀 기능이 겹치는 느낌

/// <summary>
/// UI의 상태를 갱신하는 컨트롤러
/// </summary>
public class UIController : Singleton<UIController>
{
    #region Serialized Fields

    /// <summary>
    /// 씬에 존재하는 UI 리스트
    /// </summary>
    [FormerlySerializedAs("L_CurUI")]
    [SerializeField] private List<BaseUI> currentUIs = new List<BaseUI>();


    #endregion

    #region Private/Protected Fields

    /// <summary>
    /// 현재 UI 상태를 설명하는 값
    /// </summary>
    /// /// <remarks>
    /// UI는 한 상태에만 고정되지 않고 여러 상황이 동시에 활성화될 수 있어 비트 플래그 방식 사용
    /// </remarks>
    private int uiState = 0;

    #endregion

    #region Properties
    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 객체 생성시 필요한, 초기화 작업 및 이벤트 등록
    /// </summary>
    /// <remarks>
    /// 처음 시작하는 씬은 무조건 타이틀이기 때문에, Main으로 설정한다.
    /// </remarks>
    protected override void Awake()
    {
        base.Awake();

        GameProgress.StartScene.AddListener(StartProtocol);

        GameProgress.EndLevel.AddListener(EndProtocol);

        Player.OnPlayerDead.AddListener(OnPlayerDeath);

        uiState |= (int)UIStateEnum.Main;
    }

    /// <summary>
    /// 처음 게임을 실행하고, Title씬으로 진입했을때, UI 상태를 갱신
    /// </summary>
    private void Start()
    {
        UpdateUIStates(uiState);
    }


    #endregion

    #region Public Methods

    /// <summary>
    /// 플레이어 사망 시 UI를 게임오버 상태로 전환.
    /// </summary>
    /// <remarks>
    /// Main 상태를 비활성화하고 GameOver 상태를 활성화한 후 UI를 갱신.
    /// </remarks>
    public void OnPlayerDeath()
    {
        DisableUIState((int)UIStateEnum.Main);
        EnableUIState((int)UIStateEnum.GameOver);
        UpdateUIStates(uiState);
    }

    /// <summary>
    /// 현재 UI 상태에 따라서, 각 UI 오브젝트의 활성화 여부를 결정
    /// </summary>
    /// <param name="uiFlag"> 현재 UI 상태 </param>
    /// <param name="active"> 활성화, 비활성화 여부 </param>
    public void UpdateUIStates(int uiFlag, bool active = true)
    {
        foreach (var ui in currentUIs)
        {
            ui.CheckActiveCondition(uiFlag, active);
        }
    }

    /// <summary>
    /// UI 오브젝트를 UI 관리 목록에 등록.
    /// </summary>
    /// <param name="uiObject"> 등록할 UI 게임 오브젝트 </param>
    public void RegisterUIList(GameObject uiObject) => currentUIs.Add(uiObject.GetComponent<BaseUI>());

    /// <summary>
    /// 해당 UI 상태 플래그를 활성화 한다.
    /// </summary>
    /// <param name="flag"> 활성화할 UI 플래그를 int형으로 변환 </param>
    public void EnableUIState(int flag) => uiState |= flag;

    /// <summary>
    /// 해당 UI 상태 플래그를 비활성화 한다.
    /// </summary>
    /// <param name="flag"> 비활성화할 UI 플래그를 int형으로 변환 </param>
    public void DisableUIState(int flag) => uiState &= ~flag;

    /// <summary>
    /// 현재 활성화된 UI 상태 플래그 값을 반환한다.
    /// </summary>
    /// <returns>현재 UI 상태를 나타내는 비트 플래그 값</returns>
    public int GetCurrentUIState() => uiState;

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 씬이 변경될때마다, CheckUI를 한다.
    /// </summary>
    protected override void StartProtocol()
    {
        if (EventSystem.current.enabled == false)
            EventSystem.current.enabled = true;

        UpdateUIStates(uiState);
    }

    /// <summary>
    /// 현재 UI 리스트를 지운다.
    /// </summary>
    protected override void EndProtocol()
    {
        currentUIs.Clear();
    }

    #endregion

}
