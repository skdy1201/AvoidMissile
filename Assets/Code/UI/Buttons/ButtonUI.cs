using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 버튼 타입에 따라 적절한 기능을 자동으로 연결
/// </summary>
/// <remarks>
/// Awake에서 자동으로 ButtonType 열거형에 따른 onClick 이벤트를 등록
/// ButtonFunction 싱글톤을 통해 게임 전반의 버튼 동작을 처리
/// </remarks>
public class ButtonUI : BaseUI, InterfaceUI
{
    #region Serialized Fields

    /// <summary>
    /// 버튼 종류에 대한 열거형
    /// </summary>
    [SerializeField] protected ButtonType type;
    #endregion

    #region Property

    public ButtonType buttonType => type;

    #endregion

    protected Button unityButton;

    #region Unity Lifecycle

    /// <summary>
    /// 부모 클래스의 UI 등록 후 버튼 이벤트를 초기화합니다.
    /// </summary>
    /// <remarks>
    /// base.Awake()에서 UI 리스트 등록이 먼저 수행
    /// Init()은 반드시 base.Awake() 호출 이후에 실행.
    /// </remarks>
    protected override void Awake()
    {
        base.Awake();
    }



    #endregion

    #region Public Methods

    /// <summary>
    /// 버튼 타입에 따라 onClick 이벤트 리스너를 등록.
    /// </summary>
    /// <remarks>
    /// Button 컴포넌트가 없을 경우 아무 동작 x.
    /// 각 ButtonType에 대응하는 ButtonFunction 메서드를 자동으로 연결.
    /// </remarks>
    public virtual void Init()
    {
        unityButton = GetComponent<Button>();
    }

    #endregion
}
