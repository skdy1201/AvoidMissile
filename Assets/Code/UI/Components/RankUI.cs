using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어의 순위별 점수를 표시하는 UI 컴포넌트
/// </summary>
/// <remarks>
/// GameData에서 본인이 가진 순위에 맞게, 점수를 가져옴.
/// </remarks>
public class RankUI : BaseUI, InterfaceUI
{
    #region Serialized Fields

    [FormerlySerializedAs("rankidx")]
    [SerializeField] private int rankIndex;

    [SerializeField] private int score;

    [SerializeField] private TextMeshProUGUI rankText;

    [FormerlySerializedAs("ScoreText")]
    [SerializeField] private TextMeshProUGUI scoreText;

    #endregion

    #region Properties

    public int Rank => rankIndex;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// BaseUI의 Awake만 동작
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// UI 초기화 인터페이스 구현. 현재 RankUI는 별도 초기화 로직이 필요하지 않음
    /// </summary>
    public void Init()
    {
    }

    /// <summary>
    /// 순위를 갱신
    /// </summary>
    /// <remarks>
    /// 순위 인덱스를 통해서 GameData에서 점수를 받아오고 순위와 점수로 데이터를 저장
    /// </remarks>
    public void RenewRank()
    {
        score = GameData.Instance.GetRank(rankIndex);
        scoreText.text = score.ToString();

        rankText.text = GetRankText();
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// rankIndex를 통해서, 순위를 표기할 문자열을 정함
    /// </summary>
    /// <returns> 인덱스를 통한 순위 </returns>
    private string GetRankText()
    {
        string result = "";

        switch (rankIndex)
        {
            case 1:
                result = "1st";
                break;
            case 2:
                result = "2nd";
                break;
            case 3:
                result = "3rd";
                break;
            case 4:
                result = "4th";
                break;
            case 5:
                result = "5th";
                break;
        }

        return result;
    }

    #endregion

}
