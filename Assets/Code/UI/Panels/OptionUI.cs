using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

/// <summary>
/// UI가 옵션상태로의 진입, 퇴장을 관리해주는 스크립트
/// </summary>
/// <remarks>
/// 옵션 값들에 대한 설정은 다른 UI들이 하지만, 
/// 옵션 애니메이션, UI들의 상태 변환을 관리
/// </remarks>
public class OptionUI : BaseUI, InterfaceUI
{
    #region Serialized Fields

    [SerializeField] private float activeTime;

    /// <summary>
    /// 옵션 상태로 진입하게 하기 위한 버튼 오브젝트
    /// </summary>
    [SerializeField] private GameObject optionButton;

    /// <summary>
    /// UI 상태가 옵션일때, 배경을 검은색으로 채워줄 객체
    /// </summary>
    [SerializeField] private GameObject optionBackGround;

    [SerializeField] private Vector2 targetSize;

    /// <summary>
    /// 옵션 UI들이 들어갈 창
    /// </summary>
    [SerializeField] private GameObject optionWindow;

    /// <summary>
    /// 옵션 상태일때, 작동할 관련 오브젝트 리스트
    /// </summary>
    [SerializeField] private List<GameObject> optionUIItem = new List<GameObject>();

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 플래그를 검사하고, 활성화 해야 한다면, 옵션들을 활성화.
    /// </summary>
    /// <param name="uiFlag"> UI 상태 </param>
    /// <param name="active"> 활성화 여부 </param>
    public override void CheckActiveCondition(int uiFlag, bool active)
    {
        base.CheckActiveCondition(uiFlag, active);

        if (this.gameObject.activeSelf && active)
        {
            ActiveOption();
        }
    }

    /// <summary>
    /// 인터페이스 함수. 옵션 UI가 초반에 필요한 것들을 설정
    /// </summary>
    public void Init()
    {
        // 이미지 오브젝트들을 다 활성화 
        optionBackGround.GetComponent<Image>().enabled = false;
        optionWindow.GetComponent<Image>().enabled = false;

        Button buttonComponent = optionButton.GetComponent<Button>();

        // null 체크
        if (buttonComponent == null)
        {
            Debug.LogError("Option Connect Fail");
            Debug.Break();
        }

        // 옵션을 바꾸든, 바꾸지 않든, CloseOption으로 결정과 동시에 옵션 종료
        foreach (var item in optionUIItem)
        {
            OptionButton optionButton = item.GetComponent<OptionButton>();

            if(optionButton != null)
            {
                item.GetComponent<Button>().onClick.AddListener(() => CloseOption());

            }
        }
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 옵션을 동작시키는 함수
    /// </summary>
    /// <remarks>
    /// 이미지들을 활성화 시킴
    /// UI 시작 애니메이션을 재생한다.
    /// </remarks>
    private void ActiveOption()
    {
        optionBackGround.GetComponent<Image>().enabled = true;
        optionWindow.GetComponent<Image>().enabled = true;

        StartCoroutine(ActiveAnimation());
    }

    /// <summary>
    /// 옵션을 비활성화 시키는 함수
    /// </summary>

    private void CloseOption()
    {
        optionBackGround.GetComponent<Image>().enabled = false;
        optionWindow.GetComponent<Image>().enabled = false;

        optionWindow.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0);

        for (int i = 0; i < optionUIItem.Count; ++i)
        {
            optionUIItem[i].SetActive(false);
        }
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// OptionUi가 활성화 하기 전에 시작할 UI 애니메이션 재생
    /// </summary>
    /// /// <remarks>
    /// 옵션 UI 표시와 함께 게임을 일시정지
    /// 게임 재개는 사용자가 옵션을 닫을 때 ButtonFunction.CloseSetting()에서 처리
    /// </remarks>
    private IEnumerator ActiveAnimation()
    {
        // 시간 구하기
        float start = Time.unscaledTime;
        float end = start + activeTime;

        // 점진적으로 커지는 옵션 창
        RectTransform rect = optionWindow.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.zero;

        // PlayScene에서는 일시 정지가 필요해 일괄적으로 timeScale 조정
        // 옵션 표시와 동시에 게임 일시정지
        Time.timeScale = 0f;

        // 시작 지점과 끝 지점 사이의 비율로 옵션 창 증가
        while (Time.unscaledTime < end)
        {
            float currentTime = Mathf.InverseLerp(start, end, Time.unscaledTime);
            float smoothTime = Mathf.SmoothStep(0f, 1f, currentTime);
            rect.sizeDelta = Vector2.Lerp(Vector2.zero, targetSize, smoothTime);

            yield return null; 
        }

        rect.sizeDelta = targetSize;

        // 옵션 창이 목표 크기 까지 도달했으므로, 오브젝트 활성화
        for (int i = 0; i < optionUIItem.Count; ++i)
            optionUIItem[i].SetActive(true);
    }

    #endregion

}
