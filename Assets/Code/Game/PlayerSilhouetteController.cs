using UnityEngine;

/// <summary>
/// Grand Missile에 가려진 플레이어의 실루엣 색상을 제어
/// </summary>
/// <remarks>
/// 안전 상태: 검정 실루엣
/// 위험 상태 (falling/homing 미사일 접근): 빨간 실루엣
/// 실루엣 렌더링 자체는 Render Objects Feature + Stencil로 처리
/// </remarks>
public class PlayerSilhouetteController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Material")]
    [SerializeField] private Material silhouetteMaterial;

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

    private static readonly int silhouetteColorId = Shader.PropertyToID("_SilhouetteColor");
    private static readonly int playerWorldPosId = Shader.PropertyToID("_PlayerWorldPos");
    private static readonly int playerClipRadiusId = Shader.PropertyToID("_PlayerClipRadius");
    private static readonly int playerClipEdgeWidthId = Shader.PropertyToID("_PlayerClipEdgeWidth");
    private static readonly int playerClipEdgeColorId = Shader.PropertyToID("_PlayerClipEdgeColor");

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        Shader.SetGlobalVector(playerWorldPosId, transform.position);
        Shader.SetGlobalFloat(playerClipRadiusId, clipRadius);
        Shader.SetGlobalFloat(playerClipEdgeWidthId, clipEdgeWidth);
        Shader.SetGlobalColor(playerClipEdgeColorId, clipEdgeColor);

        if (silhouetteMaterial == null)
            return;

        bool danger = MissileSpawner.Instance != null
            && MissileSpawner.Instance.CheckPlayerDanger(transform.position, fallingDangerRadius, homingDangerRadius);

        silhouetteMaterial.SetColor(silhouetteColorId, danger ? dangerColor : safeColor);
    }

    #endregion
}
