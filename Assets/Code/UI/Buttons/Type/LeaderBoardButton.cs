using UnityEngine;

/// <summary>
/// 리더보드 관련 함수와 연결되는 ButtonUI
/// </summary>
public class LeaderBoardButton : ButtonUI
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
            case ButtonType.LeaderBoard:
                unityButton.onClick.AddListener(LeaderBoard);
                break;
            case ButtonType.LeaderBoardClose:
                unityButton.onClick.AddListener(CloseLeaderBoard);
                break;
        }
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 리더보드 UI
    /// </summary>
    /// <remarks>
    /// UI 스테이트에 따라 자연스럽게 리더보드  UI가 활성화 된다.
    /// </remarks>
    private void LeaderBoard()
    {
        UIController.Instance.DisableUIState((int)UIStateEnum.Main);
        UIController.Instance.EnableUIState((int)UIStateEnum.LeaderBoard);

        UIController.Instance.UpdateUIStates(UIController.Instance.GetCurrentUIState());
    }

    /// <summary>
    /// 리더보드 UI를 닫는 함수
    /// </summary>
    private void CloseLeaderBoard()
    {
        UIController.Instance.DisableUIState((int)UIStateEnum.LeaderBoard);
        UIController.Instance.EnableUIState((int)UIStateEnum.Main);

        UIController.Instance.UpdateUIStates(UIController.Instance.GetCurrentUIState());
    }

    #endregion
}
