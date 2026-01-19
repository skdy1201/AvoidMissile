using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬 변경 관련 함수와 연결되는 버튼UI
/// </summary>
public class ChangeSceneButton : ButtonUI
{
    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    /// <summary>
    /// 보상형 광고 이후, 비활성화된 버튼을 다시 활성화
    /// </summary>
    private void OnEnable()
    {
        if(unityButton.enabled == false)
            unityButton.enabled = true;
    }

    #endregion

    #region Public Methods

    public override void Init()
    {
        base.Init();

        switch (type)
        {
            case ButtonType.Retry:
                unityButton.onClick.AddListener(() => SceneController.Instance.RestartPlayScene());
                GoogleMobileAdsController.AfterRewardFinished.AddListener(OffButton);
                break;
            case ButtonType.RetryTest:
                unityButton.onClick.AddListener(() => SceneController.Instance.RestartTestScene());
                break;
            case ButtonType.Title:
                unityButton.onClick.AddListener(() => SceneController.Instance.ChangeScene());
                GoogleMobileAdsController.AfterRewardFinished.AddListener(OffButton);
                break;
            case ButtonType.Play:
                unityButton.onClick.AddListener(() => SceneController.Instance.ChangeScene());
                break;
            case ButtonType.Edit:
                unityButton.onClick.AddListener(() => SceneController.Instance.ChnageTestScnen());
                break;
            case ButtonType.Exit:
                unityButton.onClick.AddListener(() => ExitGame());
                break;
        }
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 게임 종료 함수
    /// </summary>
    /// <remarks>
    /// 에디터에서 실행 할 땐, play mode를 종료 하고,
    /// 실제 어플리케이션이라면 종료
    /// </remarks>
    private void ExitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 유니티 에디터에서 실행 중일 때
        #else
            Application.Quit(); // 일반 빌드에서 실행 중일 때
        #endif
    }

    /// <summary>
    /// GoogleAdmob의 보상형 광고 이후, 부활까지의 대기시간 동안 비활성화
    /// </summary>
    private void OffButton()
    {
        Debug.Log("offButton");
        unityButton.enabled = false;
    }

    #endregion

}
