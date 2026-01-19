using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// UI 스크립트의 가장 최상위 스크립트
/// </summary>
/// <remarks>
/// UI 컨트롤러의 활성화 UI 리스트에 자동 등록
/// UI 컨트롤러가 관리하는 UI State에 따라 오브젝트 상태 갱신
/// </remarks>
public class BaseUI : MonoBehaviour
{
    #region Serialized Fields

    [FormerlySerializedAs("ActiveFlag")]
    [SerializeField] protected int activeFlag;

    #endregion

    #region Properties

    public int ActiveFlag => activeFlag;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 부모 오브젝트에 Canvas가 있으면, UI 오브젝트이기 때문에, UI 리스트에 등록
    /// </summary>
    virtual protected void Awake()
    {
        if (this.gameObject.transform.parent.GetComponent<Canvas>() != null)
            UIController.Instance.RegisterUIList(this.gameObject);
    }

    #endregion


    #region Public Methods

    /// <summary>
    /// 플래그와 활성화 여부에 따라서, 오브젝트의 상태를 조정
    /// </summary>
    /// <param name="uiFlag"> UI 플래그 </param>
    /// <param name="shouldActivate"> 활성화 여부 </param>
    public virtual void CheckActiveCondition(int uiFlag, bool shouldActivate)
    {
        //// 비트 플래그 연산으로 다중 UI 상태 관리
        bool flagMatches = (uiFlag & activeFlag) != 0;
        bool shouldBeActive = shouldActivate ? flagMatches : !flagMatches;

        if (gameObject.activeSelf != shouldBeActive)
        {
            gameObject.SetActive(shouldBeActive);
        }
    }

    #endregion


}
