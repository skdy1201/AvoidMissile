using UnityEngine;

/// <summary>
/// Grand Missile에 가려진 플레이어의 가시성을 제어
/// Render Objects Feature + Stencil 클리핑 방식
/// </summary>
public class PlayerVisibilityController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Grand Missile Clip")]
    [SerializeField] private float clipRadius = 3f;
    [SerializeField] private float clipEdgeWidth = 0.3f;
    [SerializeField] private Color clipEdgeColor = Color.red;

    #endregion

    #region Private Fields

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
    }

    #endregion
}
