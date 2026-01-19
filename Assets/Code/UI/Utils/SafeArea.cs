using UnityEngine;

/// <summary>
/// UI들의 배치를 조정해주는 스크립트
/// </summary>
/// <remarks>
/// 기기마다 다른 해상도에서 비율을 조정하다 UI배치가 고르지 못한 상황을 수정
/// </remarks>
public class SafeArea : BaseUI, InterfaceUI
{
    #region Private/Protected Fields

    private RectTransform safeArea;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    #endregion

    /// <summary>
    /// 레터박스를 추가하는 CameraController class에 이벤트 등록
    /// </summary>
    /// <remarks>
    /// 레터박스 설정이 끝나면 이벤트 invoke
    /// </remarks>
    public void Init()
    {
        CameraController.SetSafeArea.AddListener(ActiveSafeArea);

    }

    /// <summary>
    /// 
    /// </summary>
    private void ActiveSafeArea()
    {
        Debug.Log("Active Safe Area");

        safeArea = GetComponent<RectTransform>();

        Rect camRectTransform = Camera.main.rect;

        //UI의 최대, 최소 범위를 카메라의 rect에 맞춘다.
        safeArea.anchorMin = new Vector2(camRectTransform.x, camRectTransform.y);
       
        safeArea.anchorMax = new Vector2(camRectTransform.x + camRectTransform.width,
                                         camRectTransform.y + camRectTransform.height);

        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
    }
}
