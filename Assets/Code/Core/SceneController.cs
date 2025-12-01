using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬 전환을 관리하는 스크립트
/// </summary>
/// <remarks>
/// 
/// </remarks>
public class SceneController : Singleton<SceneController>
{

    #region Private/Protected Methods

    protected override void StartProtocol()
    {
    }

    protected override void EndProtocol()
    {
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        GameProgress.StartScene.AddListener(StartProtocol);

        GameProgress.EndLevel.AddListener(EndProtocol);

    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 씬을 변경해주는 함수
    /// </summary>
    /// <remarks>
    /// TitleScene과 PlayScene 간 전환을 처리하며, 씬 전환 중 중복 클릭을 방지하기 위해 버튼을 비활성화
    /// GameProgress.EndLevel 이벤트를 호출하고 사운드 페이드 아웃을 적용
    /// UI 차단 상태를 활성화해 다른 UI들의 동작을 비활성화.
    /// </remarks>
    public void ChangeScene()
    {

        Time.timeScale = 1.0f;
        UIController.Instance.DisableUIState((int)UIStateEnum.GameOver);
        UIController.Instance.EnableUIState((int)UIStateEnum.Main);
        UIController.Instance.EnableUIState((int)UIStateEnum.Block);

        UIController.Instance.CheckBlock();

        GameProgress.EndLevel?.Invoke();

        if (SceneManager.GetActiveScene().name == GlobalData.Instance.TitleScene)
            StartCoroutine(SceneTransition(GameProgress.Instance.SoundFadeTime, GlobalData.Instance.PlayScene));
        else if (SceneManager.GetActiveScene().name == GlobalData.Instance.PlayScene || SceneManager.GetActiveScene().name == GlobalData.Instance.DevTestScene)
            StartCoroutine(SceneTransition(GameProgress.Instance.SoundFadeTime, GlobalData.Instance.TitleScene));

    }

    /// <summary>
    /// PlayScene 재시작 함수
    /// </summary>
    /// <param name="buttonObject"> 재시작 버튼 </param>
    public void RestartPlayScene(GameObject buttonObject)
    {
        GameProgress.EndLevel?.Invoke();

        // 플레이어 사망시 timescale이 0
        Time.timeScale = 1.0f;

        // UI 플래그 조정
        UIController.Instance.EnableUIState((int)UIStateEnum.Main);
        UIController.Instance.DisableUIState((int)UIStateEnum.GameOver);

        // PlayScene 예약
        StartCoroutine(reserveProtocol(GlobalData.Instance.PlayScene));

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        buttonObject.GetComponent<Button>().enabled = false;

    }

    public void RestartTestScene()
    {
        BoomEffectSpawner.Instance.SelfEndProtocol();

        Time.timeScale = 1.0f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    }

    #endregion

    #region Coroutines

    /// <summary>
    /// 씬 전환을 한 후, StartProtocol을 작동시키기 위한 코루틴
    /// </summary>
    /// <param name="targetScene"> 타겟 씬 </param>
    IEnumerator reserveProtocol(string targetScene)
    {
        // 이름을 비교하며 바꾸기 전까지 대기
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == targetScene);

        // 각 싱글톤 객체들의 초기화 동작 시작
        if (GameProgress.StartScene != null)
        {
            GameProgress.StartScene?.Invoke();
        }
    }

    /// <summary>
    /// 일정한 시간을 통해 씬 바꾸기 
    /// </summary>
    /// <param name="time"> 씬 전환시간 </param>
    /// <param name="strScene"> 전환하는 씬 이름 </param>
    /// <returns></returns>
    // TODO: AudioContoller의 fadeout 함수와 연계하는게 좋을지도
    public IEnumerator SceneTransition(float time, string strScene)
    {
        float curTime = time;

        while (curTime > 0)
        {
            curTime -= Time.unscaledDeltaTime;

            float ratio = Mathf.Lerp(GameData.Instance.GetSettingValue(OptionType.Bgm), 0.0f, (time - curTime) / time);

            AudioController.Instance.SetBGMvolume(ratio);
            yield return new WaitForSecondsRealtime(Time.unscaledDeltaTime);
        }

        SceneManager.LoadScene(strScene);

        StartCoroutine(reserveProtocol(strScene));
    }

    #endregion

}
