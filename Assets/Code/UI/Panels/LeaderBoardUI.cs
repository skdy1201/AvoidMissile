using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// 게임 내 플레이어 순위를 표시하는 리더보드 UI 관리
/// </summary>
/// <remarks>
/// UI 활성화 시 자동으로 최신 순위 정보를 갱신.
/// RankUI 컴포넌트들을 관리
/// </remarks>
public class LeaderBoardUI : BaseUI, InterfaceUI
{
    #region Serialized Fields

    [SerializeField] private RankUI rankUIPrefab;

    #endregion

    #region Private/Protected Fields

    /// <summary>
    /// rank UI 오브젝트들을 담는 리스트
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    private List<RankUI> rankUIs = new List<RankUI>();

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 리더보드 UI 스크립트의 Awake
    /// </summary>
    /// <remarks>
    /// 현재 UI 리스트에 등록해둔다.
    /// 자식 오브젝트로 존재하는 RankUI들을 리스트로 정렬해서 관리해둔다.
    /// getname mainthread 버그로 인해 변경된 구조
    /// </remarks>
    protected override void Awake()
    {
        base.Awake();

        // 자식 RankUI 컴포넌트 모두 찾기
        rankUIs = new List<RankUI>(GetComponentsInChildren<RankUI>());

        // rankIndex 순서대로 정렬 (1~5라면)
        rankUIs.Sort((a, b) => a.Rank.CompareTo(b.Rank));

    }

    #endregion

    #region Public Methods

    /// <summary>
    /// UI 초기화 작업을 수행.
    /// </summary>
    /// <remarks>
    /// InterfaceUI 인터페이스 구현을 위해 존재. 
    /// 현재는 Awake에서 초기화가 완료.
    /// </remarks>
    public void Init()
    {
    }

    /// <summary>
    /// 활성화 상태라면, 랭크 리스트들이 랭크를 갱신하도록 한다.
    /// </summary>
    /// <param name="uiFlag"> UI 상태를 나타내는 플래그 값 </param>
    /// <param name="active"> UI 활성화 여부 </param>
    public override void CheckActiveCondition(int uiFlag, bool active)
    {
        base.CheckActiveCondition(uiFlag, active);

        if (this.gameObject.activeSelf)
        {
            RenewRank();
        }
    }

    public void AddRankUIs(int listIndex, RankUI rankUIobject)
    {
        rankUIs[listIndex] = rankUIobject;
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 모든 RankUI 컴포넌트의 순위 정보를 갱신합니다
    /// </summary>
    private void RenewRank()
    {
        foreach (var rank in rankUIs)
        {
            rank.RenewRank();
        }
    }

    #endregion

}
