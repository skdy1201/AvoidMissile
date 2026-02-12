using UnityEngine;

/// <summary>
/// Grand Missile에 가려진 플레이어의 가시성을 제어
/// </summary>
/// <remarks>
/// 안전 상태: 검정 오버레이
/// 위험 상태 (falling/homing 미사일 접근): 빨강 오버레이
/// 오버레이 렌더링 자체는 Render Objects Feature + Stencil로 처리
/// </remarks>
public class PlayerVisibilityController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Material")]
    [SerializeField] private Material visibilityMaterial;

    [Header("Danger Detection")]
    [SerializeField] private float fallingDangerRadius = 2f;
    [SerializeField] private float homingDangerRadius = 3f;

    [Header("Colors")]
    [SerializeField] private Color safeColor = Color.black;
    [SerializeField] private Color dangerColor = Color.red;

    [Header("Grand Missile Clip")]
    [SerializeField] private float clipRadius = 3f;
    [SerializeField] private float clipEdgeWidth = 0.3f;
    [SerializeField] private Color clipEdgeColor = Color.red;

    #endregion

    #region Private Fields

    private static readonly int visibilityColorId = Shader.PropertyToID("_VisibilityColor");
    private static readonly int playerWorldPosId = Shader.PropertyToID("_PlayerWorldPos");
    private static readonly int playerClipRadiusId = Shader.PropertyToID("_PlayerClipRadius");
    private static readonly int playerClipEdgeWidthId = Shader.PropertyToID("_PlayerClipEdgeWidth");
    private static readonly int playerClipEdgeColorId = Shader.PropertyToID("_PlayerClipEdgeColor");
    private static readonly int gameCamPosId = Shader.PropertyToID("_GameCamPos");

    private Camera cachedCam;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        Shader.SetGlobalVector(playerWorldPosId, transform.position);
        Shader.SetGlobalFloat(playerClipRadiusId, clipRadius);
        Shader.SetGlobalFloat(playerClipEdgeWidthId, clipEdgeWidth);
        Shader.SetGlobalColor(playerClipEdgeColorId, clipEdgeColor);

        if (cachedCam == null)
        {
            cachedCam = Camera.main;
            if (cachedCam == null)
                cachedCam = FindObjectOfType<Camera>();
        }

        if (cachedCam != null)
        {
            Shader.SetGlobalVector(gameCamPosId, cachedCam.transform.position);
        }

        if (visibilityMaterial == null)
            return;

        bool danger = MissileSpawner.Instance != null
            && MissileSpawner.Instance.CheckPlayerDanger(transform.position, fallingDangerRadius, homingDangerRadius);

        visibilityMaterial.SetColor(visibilityColorId, danger ? dangerColor : safeColor);
    }

    #endregion
}
