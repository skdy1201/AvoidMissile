using GoogleMobileAds;
using GoogleMobileAds.Api;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.Events;

/// <summary>
/// Google AdMob 외부 SDK를 사용할 수 있도록 도와주는 컨트롤러
/// </summary>
/// <remarks>
/// 처음 시작할 때 광고 오브젝트를 초기화
/// 조건에 따라 광고를 생성, 재생, 파괴
/// </remarks>
public class GoogleMobileAdsController : Singleton<GoogleMobileAdsController>
{
    #region Serialized Fields

    [Header("TestAdId")]
    [SerializeField] private bool testModeEnabled = false;

    [Header("Ad Finish Flag")]
    [SerializeField] private bool finishinterstitialAd = false;
    [SerializeField] private bool finishRewardedAd = false;

    [SerializeField] private bool closeRewardAd = false;

    #endregion

    #region Private/Protected Fields

    private InterstitialAd interstitialAd;

    private RewardedAd rewardedAd;
    
    // 광고 이후 콜백이 늦게 돌아와 게임이 다시 시작했음에도 불구하고, 정지되는 것을 방지
    private bool validPause = false;

    // 광고 로딩 실패시, 로드횟수 제한
    private int currentRetryCount = 0;
    private const int maxRetryCount = 3;

    #endregion

    #region Property

    public bool ValidPause
    {
        get { return validPause; }
        set { validPause = value;}
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 광고 오브젝트 초기화
    /// </summary>
    /// <remarks>
    /// 타이틀 씬에선 광고를 재생할 필요가 없기 때문에,
    /// 플레이 씬에서만 광고를 로드한다.
    /// </remarks>
    protected override void Awake()
    {
        base.Awake();

        GameProgress.StartScene.AddListener(() => StartProtocol());

        MobileAds.Initialize((InitializationStatus initStatus) =>
        {
            if (initStatus == null)
            {
                Debug.LogError("Google Mobile Ads initialization failed.");
                return;
            }
        });
        
        StartCoroutine(AdmobChecker());
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 외부에서 광고 재생을 하기 위한 래퍼 함수
    /// </summary>
    public void DisplayInterstitialAd()
    {
        ShowInterstitialAd();
    }

    /// <summary>
    /// 리워드 광고를 외부에서 재생할 수 있는 래퍼 함수
    /// </summary>
    public void DisplayRewardAd()
    {
        ShowRewardAd();
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 플레이 씬이라면, 광고 로드
    /// </summary>
    protected override void StartProtocol()
    {
        if (SceneManager.GetActiveScene().name == GlobalData.Instance.PlayScene)
        {
            LoadInterstitialAd();
            LoadRewardedAd();
        }
    }

    /// <summary>
    /// 현재는 끝날때 할 동작이 없음
    /// </summary>
    protected override void EndProtocol()
    {
    }

    /// <summary>
    /// 전면광고 로드
    /// </summary>
    /// <remarks>
    /// 테스트 버전에선 테스트 광고 ID를 사용하기 위해 내부적으로 체크
    /// </remarks>
    private void LoadInterstitialAd()
    {
        // Create our request used to load the ad.
        var adRequest = new AdRequest();
        string adUnitId;

        if (testModeEnabled)
        {
            adUnitId = "ca-app-pub-3940256099942544/1033173712";
        }
        else
        {
            adUnitId = "ca-app-pub-4152423074686548/3054849538";
        }

        // Send the request to load the ad.
        InterstitialAd.Load(adUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null)
            {
                // The ad failed to load.
                return;
            }
            // The ad loaded successfully.

            interstitialAd = ad;

            // 광고가 끝날 때, 처리할 이벤트들 등록
            interstitialAd.OnAdFullScreenContentClosed += () => finishinterstitialAd = true;
            interstitialAd.OnAdFullScreenContentClosed += () => ReleasedinterstitialAd();
        });
  

    }

    /// <summary>
    /// 보상형 광고 로드 함수
    /// </summary>
    /// <remarks>
    /// 보상형 광고 로드 횟수를 제한하라는 가이드에 맞춰
    /// 실패시 3번 재시도
    /// </remarks>
    private void LoadRewardedAd()
    {
        string adUnitId;

        if (testModeEnabled)
            adUnitId = "ca-app-pub-3940256099942544/5224354917";
        else
            adUnitId = "ca-app-pub-4152423074686548/8826235366";

        var adRequest = new AdRequest();

        RewardedAd.Load(adUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null)
            {
                currentRetryCount++;

                if (currentRetryCount < maxRetryCount)
                {
                    Debug.LogWarning("Rewarded Ad load Failed");

                    LoadRewardedAd();
                }
                else
                {
                    currentRetryCount = 0;
                    Debug.LogError($"Rewarded Ad load failed after {maxRetryCount} retries: { error.GetMessage()}");
                }

                return;
            }

            // ad loaded successfully
            rewardedAd = ad;
            currentRetryCount = 0;

            RegisterRewardedAdEvents();

        });
    }

    /// <summary>
    /// 전면광고 재생
    /// </summary>
    private void ShowInterstitialAd()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            Debug.Log("Show Ad");
            validPause = true;
            interstitialAd.Show();
        }
    }

    /// <summary>
    /// AdMob 홈페이지에서 설정한 Reward를 체크해 보상 제공
    /// </summary>
    private void ShowRewardAd()
    {
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            Debug.Log("Show Rewarded Ad");

            rewardedAd.Show((Reward reward) =>
            {

                Debug.Log("in reward");

                if(reward.Type == "Revive" && reward.Amount == 1)
                {
                    Debug.Log("Reward Check Sucesses");
                    validPause = true;
                    finishRewardedAd = true;
                }
            });
        }

    }

    private void ReleasedinterstitialAd()
    {
        DestroyInterstitialAd();
    }

    /// <summary>
    /// 광고 끝난 이후, 해당 광고 제거
    /// </summary>
    /// <remarks>
    /// TimeScale  조정을 위한 코루틴 체크
    /// </remarks>
    private void DestroyInterstitialAd()
    {
        // [START destroy_ad]
        if (interstitialAd != null)
        {

            interstitialAd.Destroy();
            interstitialAd = null;
            Debug.Log("Ad Destroy");
        }
        // [END destroy_ad]]

    }

    /// <summary>
    /// 광고 끝난 이후, 해당 광고 제거
    /// </summary>
    private void ReleaseRewardedAd()
    {

        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

    }

    // 리워드 광고 이벤트 연결
    private void RegisterRewardedAdEvents()
    {
        rewardedAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[GoogleMobileAds] Rewarded ad closed.");
            ReleaseRewardedAd();
            LoadRewardedAd(); // 다음 시청을 위해 재로드

            closeRewardAd = true;

        };

        rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"[GoogleMobileAds] Rewarded ad failed to show: {error.GetMessage()}");
            LoadRewardedAd(); // 실패 시 재로드
        };
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// 광고 이후, 부활 시간 전까지, Title, Retry 버튼의 상호작용 방지 이벤트
    /// </summary>
    public static UnityEvent AfterRewardFinished = new UnityEvent();

    #endregion

    #region Coroutine

    /// <summary>
    /// Admob이 임의로 TimeScale을 조종하기 때문에, 코루틴으로 검사하며, 상황마다 TimeScale을 의도대로 통제
    /// </summary>
    /// <remarks>
    /// 전면 광고는 끝난 이후, 일시 정지
    /// 보상 광고는 광고가 끝나자 마자, 부활효과가 작동하기 때문에, 일시 정지 후, 부활 시작
    /// </remarks>
    IEnumerator AdmobChecker()
    {
        while(true)
        {
            yield return new WaitForSecondsRealtime(0.1f);

            if(finishinterstitialAd == true)
            {
                if(validPause)
                    Time.timeScale = 0f;

                finishinterstitialAd = false;
            }
            else if(finishRewardedAd == true)
            {
                // 재부활을 하지 못하도록 미리 세팅
                Player player = GlobalData.Instance.Player.GetComponent<Player>();
                player.Revive = true;
                GameProgress.Instance.PlayerAlive = true;

                if(validPause)
                    Time.timeScale = 0f;

                finishRewardedAd = false;

            }
            else if(closeRewardAd == true)
            {
                closeRewardAd = false;
                UIController.Instance.ReviveUIController();

            }
        }
    }
    #endregion
}
