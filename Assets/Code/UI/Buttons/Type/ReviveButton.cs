using UnityEngine;

/// <summary>
/// 보상형 광고 시청과 연결되는 ButtonUI
/// </summary>
public class ReviveButton : ButtonUI
{
    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    private void OnEnable()
    {
        if (type == ButtonType.Revive && GameProgress.Instance.PlayerAlive)
            gameObject.SetActive(false);
    }

    #endregion

    #region Public Methods

    public override void Init()
    {
        base.Init();

        switch (type)
        {
            case ButtonType.Revive:
                unityButton.onClick.AddListener(ReviveAdvertise);
                break;
        }
    }


    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 보상형 광고 재생 전 사전 준비 및 광고 시청
    /// </summary>
    public void ReviveAdvertise()
    {
        Time.timeScale = 0f;
        GoogleMobileAdsController.AfterRewardFinished.Invoke();
        GoogleMobileAdsController.Instance.DisplayRewardAd();
    }

    #endregion
}
