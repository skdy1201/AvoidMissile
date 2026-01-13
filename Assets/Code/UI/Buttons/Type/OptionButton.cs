using UnityEngine;

/// <summary>
/// 옵션 변경 관련 함수와 연결되는 버튼UI
/// </summary>
public class OptionButton : ButtonUI
{
    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    #endregion

    #region Public Methods

    public override void Init()
    {
        base.Init();

        switch (type)
        {
            case ButtonType.Option:
                unityButton.onClick.AddListener(() => SettingGame());
                break;
            case ButtonType.OptionAccept:
                unityButton.onClick.AddListener(() => AcceptOption());
                break;
            case ButtonType.OptionClose:
                unityButton.onClick.AddListener(() => CloseSetting());
                break;
        }
    }

    #endregion


    #region Private/Protected Methods

    /// <summary>
    /// UI 상태를 갱신하고, UI 컨트롤러에게 UI 현재 활성화 UI를 갱신
    /// </summary>
    private void SettingGame()
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
    private void CloseSetting()
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
    private void AcceptOption()
    {
        GameData.Instance.SetOption();

        CloseSetting();

    }

    #endregion

}
