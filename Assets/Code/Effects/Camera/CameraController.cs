using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 화면 비율을 16:9로 고정하고, 다른 비율의 화면에는 레터박스를 추가.
/// </summary>
/// <remarks>
/// 화면이 16:9보다 넓으면 좌우에, 좁으면 상하에 검은 여백 생성
/// </remarks>
public class CameraController : MonoBehaviour
{

    #region Private/Protected Fields

    private const float TargetAspectRatio = 16f / 9f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Camera mainCamera = GetComponent<Camera>();

        // 종현재 화면 비율과 목표 비율(16:9)을 비교하여 스케일 계산
        Rect cameraRect = mainCamera.rect;

        float heightScale = ((float)Screen.width / Screen.height) / TargetAspectRatio;
        float widthScale = 1f / heightScale;

        // 화면이 16:9보다 세로로 긴 경우 상하에, 가로로 긴 경우 좌우에 레터박스 추가
        if (heightScale < 1)
        {
            cameraRect.height = heightScale;
            cameraRect.y = (1f - heightScale) / 2f;
        }
        else
        {
            cameraRect.width = widthScale;
            cameraRect.x = (1f - widthScale) / 2f;
        }

        mainCamera.rect = cameraRect;

        Debug.Log($"mainCamera's rect is {cameraRect}");

        ChangeCameraRect.Invoke();
    }

    #endregion

    #region Event Handlers

    static public UnityEvent ChangeCameraRect = new UnityEvent();

    #endregion

}
