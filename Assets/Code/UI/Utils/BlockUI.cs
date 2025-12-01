using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주어진 영역 내에 존재하는 UI들의 동작을 막는 UI
/// </summary>
/// <remarks>
/// 이미지를 갖고, ui controller의 플래그에 따라서 해당 영역 내에 있는 ui들을 비활성화
/// 현재로서는, 모든 ui들의 동작을 막는 역할만 수행하기 때문에, UI controller에서 하나만 매칭
/// </remarks>
public class BlockUI : BaseUI, InterfaceUI
{

    #region Private/Protected Fields

    // 동작을 막을 영역을 지정하는 투명 이미지
    private Image blockImage;

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
    /// 이미지를 캐싱해두고, UI 컨트롤러에 BLOCK UI를 등록해둔다.
    /// </summary>
    public void Init()
    {
        blockImage = GetComponent<Image>();
        UIController.Instance.RegisterBlock(this);
    }

    /// <summary>
    /// 해당 ui의 좌표가 영역 내에 있는지 없는지 감지하는 함수
    /// </summary>
    /// <param name="targetUIPosition"> 해당 UI의 위치 </param>
    /// <returns> 영역 내에 있는지 계산한 결과 </returns>
    public bool InRange(Vector2 targetUIPosition)
    {
        RectTransform imageRect = blockImage.GetComponent<RectTransform>();

        float halfWidth = imageRect.rect.width / 2f;
        float halfHeight = imageRect.rect.height / 2f;

        bool inRangeX = targetUIPosition.x >= imageRect.anchoredPosition3D.x - halfWidth
                       && targetUIPosition.x <= imageRect.anchoredPosition3D.x + halfWidth;
        bool inRangeY = targetUIPosition.y >= imageRect.anchoredPosition3D.y - halfHeight
                        && targetUIPosition.y <= imageRect.anchoredPosition3D.y + halfHeight;

        return inRangeX && inRangeY;
    }

    #endregion

}
