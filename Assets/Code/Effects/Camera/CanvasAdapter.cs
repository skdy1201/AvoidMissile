using UnityEngine;

public enum UIPosition
{
    Left,
    Right,
    Top,
    Bottom,
    LeftTop,
    LeftBottom,
    RightTop,
    RightBottom
}

/// <summary>
/// CameraController로 바뀐 Rect에 맞춰 해당 UI의 Anchor를 조정
/// </summary>
public class CanvasAdapter : MonoBehaviour
{

    /// <summary>
    /// CameraController에서 조정이 끝나면 호출할 이벤트를 등록
    /// </summary>
    private void OnEnable()
    {
        CameraController.ChangeCameraRect.AddListener(()=>AdaptAnchor());
    }

    /// <summary>
    /// 변한 카메라의 Rect에 따라, Anchor를 재설정해서 레터박스가 생기더라도 Overlay상태처럼 UI 위치를 조정
    /// </summary>
    private void AdaptAnchor()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("Camera match Fail");
        }

        RectTransform rectTransform = GetComponent<RectTransform>();
        Vector2 curPosition = rectTransform.position;

        Rect cameraRect = mainCamera.rect;

       // // 카메라 rect를 그대로 앵커로 적용
       // rectTransform.anchorMin = new Vector2(CameraRect.x, CameraRect.y);
       // rectTransform.anchorMax = new Vector2(
       //     CameraRect.x + CameraRect.width,
       //     CameraRect.y + CameraRect.height);

        Debug.Log(rectTransform.anchorMin);
        Debug.Log(rectTransform.anchorMax);

    }


}
