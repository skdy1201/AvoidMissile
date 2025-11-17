using GoogleMobileAds;
using GoogleMobileAds.Api;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

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

    [FormerlySerializedAs("useTestAds")]
    [SerializeField] private bool testModeEnabled = false;

    #endregion

    #region Private/Protected Fields

    private InterstitialAd interstitialAd;

    #endregion

    #region Properties
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
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 외부에서 광고 재생을 하기 위한 래퍼 함수
    /// </summary>
    public void DisplayAd()
    {
        ShowAd();
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
            LoadAd();
        }
    }

    /// <summary>
    /// 현재는 끝날때 할 동작이 없음
    /// </summary>
    protected override void EndProtocol()
    {
    }

    /// <summary>
    /// 광고 로드
    /// </summary>
    /// <remarks>
    /// 테스트 버전에선 테스트 광고 ID를 사용하기 위해 내부적으로 체크
    /// </remarks>
    private void LoadAd()
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
            interstitialAd.OnAdFullScreenContentOpened += () => AudioController.Instance.PauseBGM();
            interstitialAd.OnAdFullScreenContentClosed += () => AudioController.Instance.PlayCurBGM();
            interstitialAd.OnAdFullScreenContentClosed += () => ReleaseAd();

        });

    }

    /// <summary>
    /// 광고 재생
    /// </summary>
    private void ShowAd()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            interstitialAd.Show();
        }
    }

    /// <summary>
    /// 광고 끝난 이후, 해당 광고 제거
    /// </summary>
    /// <remarks>
    /// TimeScale  조정을 위한 코루틴 체크
    /// </remarks>
    private void ReleaseAd()
    {
        // [START destroy_ad]
        if (interstitialAd != null)
        {

            interstitialAd.Destroy();
            interstitialAd = null;
            Debug.Log("Ad Destroy");
            Debug.Log($"TimeScale is : {Time.timeScale}");

            // 다음 프레임과 그 다음 프레임도 체크
            StartCoroutine(RestoreTimeScale());

        }
        // [END destroy_ad]]

    }

    #endregion


    #region Coroutines

    /// <summary>
    /// GoogleAdmob이 TimeScale을 임의로 조정해서 코루틴으로 체크
    /// </summary>
    /// <remarks>
    /// 플레이어가 죽은 시점이기 때문에, timescale을 다시 0으로 만들어둔다.
    /// </remarks>
    private IEnumerator RestoreTimeScale()
    {
        yield return new WaitUntil(() => Time.timeScale == 1f);

        Time.timeScale = 0f;
    }

    #endregion

}
