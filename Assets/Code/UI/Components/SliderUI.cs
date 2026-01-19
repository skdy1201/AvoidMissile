using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;   

public enum OptionType
{
    None,
    Alpha,
    Bgm,
    EffectSound,
}

//TODO : OnEnable , 슬라이더 실시간 싱크?
// Project 검토 참고


/// <summary>
/// 슬라이더 UI
/// </summary>
/// <remarks>
/// 열거형 타입과 슬라이더의 값을 연결
/// 슬라이더의 변화를 관리
/// </remarks>
public class SliderUI : BaseUI, InterfaceUI
{
    #region Serialized Fields

    [Header("Slider Type")]
    [SerializeField] OptionType sliderType;

    [Header("Slider Value")]

    /// <summary>
    /// 디버그용 직렬화 변수 및 GameData와 Slider를 연결해주는 중간 변수
    /// </summary>
    [SerializeField] float sliderValue;

    [Header("Connect UI")]

    [SerializeField] private Slider slider;

    [SerializeField] private Button acceptButton;

    [SerializeField] private Button cancelButton;

    #endregion

    #region Private/Protected Fields

    /// <summary>
    /// 슬라이더 값과 fvalue를 동기화 시키기 위한 변수
    /// </summary>
    private bool isInitialized = false;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    /// <summary>
    /// 처음 동작이 된다면, 슬라이더의 값을 GameData에서 받아와서 동기화 시킴.
    /// </summary>
    void Update()
    {
        if (isInitialized == false && this.gameObject.activeSelf)
        {
            isInitialized = true;
            SyncSlider();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 슬라이더 타입에서 값을 받아오고,
    /// 저장 버튼과 취소 버튼의 이벤트 등록
    /// </summary>
    public void Init()
    {
        sliderValue = GameData.Instance.GetSettingValue(sliderType);

        slider.value = sliderValue;

        if (acceptButton != null)
            acceptButton.onClick.AddListener(() => SaveChnageValue());

        if (cancelButton != null)
            cancelButton.onClick.AddListener(() => CancelChanges());
    }


    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 바꾼 슬라이더 값을 저장.
    /// </summary>
    private void SaveChnageValue()
    {
        sliderValue = slider.value;

        sliderValue = Mathf.Floor(sliderValue * 100) / 100f;
        GameData.Instance.SetSettingValue(sliderType, sliderValue);
        isInitialized = false;
    }

    /// <summary>
    /// 취소 버튼을 눌럿을 때, 값을 기존값을 그대로 유지.
    /// </summary>
    private void CancelChanges()
    {
        slider.value = sliderValue;
        GameData.Instance.SetSettingValue(sliderType, sliderValue);
        isInitialized = false;

    }

    /// <summary>
    /// 슬라이더의 값을 기존에 저장했던 값과 동기화 시킴.
    /// </summary>
    private void SyncSlider()
    {
        slider.value = GameData.Instance.GetSettingValue(sliderType);
    }

    #endregion

}
