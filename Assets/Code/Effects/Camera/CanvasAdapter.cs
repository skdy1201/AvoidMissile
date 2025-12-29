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
/// CameraController로 바뀐 Rect에 맞춰 해당 UI의 위치를 조정
/// </summary>
public class CanvasAdapter : MonoBehaviour
{

    #region Unity Lifecycle

    /// <summary>
    /// CameraController에서 조정이 끝나면 호출할 이벤트를 등록
    /// </summary>
    private void OnEnable()
    {
        CameraController.ChangeCameraRect.AddListener(AdaptAnchor);
    }

    #endregion

    #region Private/Protected Methods

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

        RectTransform uiRect = GetComponent<RectTransform>();
        Vector3 uiPosition = uiRect.position;

        if (uiRect == null)
        {
            Debug.Log("RectTransform is null");
        }

        Rect cameraRect = mainCamera.rect;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);

        // x 위치 조정
        // cameraRect의 값의 절반이 각각의 레터박스
        // 기존의 해상도 * 절반의 x Rect 값을 곱해 변경된 해상도 크기
        // 중앙을 기준으로 좌우 이동 여부 결정
        if (cameraRect.x > 0)
        {
            float xMove = (cameraRect.x / 2f) * screenSize.x;

            if (uiRect.rect.x < 0)
            {
                uiPosition.x += xMove;
            }
            else
            {
                uiPosition.x -= xMove;
            }
        }

        // y 위치 조정
        // cameraRect의 값의 절반이 각각의 레터박스
        // 기존의 해상도 * 절반의 y Rect 값을 곱해 변경된 해상도 크기
        // 중앙을 기준으로 상하 이동 여부 결정
        if (cameraRect.y > 0)
        {
            float yMove = (cameraRect.y / 2f) * screenSize.y;

            if (uiRect.rect.y < 0)
            {
                uiPosition.y += yMove;
            }
            else
            {
                uiPosition.y -= yMove;
            }
        }

        uiRect.position = uiPosition;
    }

    #endregion

}
