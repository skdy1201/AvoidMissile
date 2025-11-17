using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public enum ScoreType
{
    Play,
    Current,
    Max,
    Rank,
}

/// <summary>
/// 플레이어의 점수
/// </summary>
public class Score : BaseUI, InterfaceUI
{
    #region Serialized Fields

    [FormerlySerializedAs("scoreUI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [SerializeField] private ScoreType scoreType;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        Init();
    }


    /// <summary>
    /// 플레이 점수라면, 매 틱마다 문자열을 업데이트
    /// </summary>
    void Update()
    {
        if (scoreType == ScoreType.Play)
            scoreText.text = GameProgress.Instance.Score.ToString();
    }

    private void OnDestroy()
    {
        Player.OnPlayerDead.RemoveListener(FloatPlayerScore);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 현재 점수와 최고 점수를 띄우는 함수
    /// </summary>
    public void FloatPlayerScore()
    {
        if (scoreType == ScoreType.Current)
            scoreText.text = $"Score : {GameProgress.Instance.Score}";
        else if (scoreType == ScoreType.Max)
            scoreText.text = $"Max Score : {GameData.Instance.GetMaxScore()}";
    }

    /// <summary>
    /// scoreText 참조가 누락된 경우를 대비하여 필드를 동기화하고,
    /// 플레이어 사망 시 점수 표시를 위한 이벤트를 등록
    /// </summary>
    public void Init() 
    {
        if (scoreText == null)
            scoreText = GetComponent<TextMeshProUGUI>();

        Player.OnPlayerDead.AddListener(() => FloatPlayerScore());
    }

    #endregion

}
