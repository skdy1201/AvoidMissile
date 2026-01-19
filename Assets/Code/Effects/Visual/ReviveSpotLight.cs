using UnityEngine;
using System.Collections;

/// <summary>
/// 부활시, 작동하는 스포트라이트
/// </summary>
public class ReviveSpotLight : MonoBehaviour
{
    #region Serialized Fields

    [Header("Timer")]
    [SerializeField] private float effectTimer;
    [SerializeField] private float offTimer;

    [Header("Setting")]
    [SerializeField] private float targetIntensity;
    [SerializeField] private float targetAngle;

    [SerializeField] private float pingpongSpeed;

    #endregion

    #region Private/Protected Fields

    private Light targetLight;

    private bool effectEnd = false;

    private GameObject player;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 초기 설정 후, 비활성화
    /// </summary>
    private void Awake()
    {
        targetLight = GetComponent<Light>();
        targetLight.innerSpotAngle = 0f;
        targetLight.spotAngle = 0f;
        GameProgress.Instance.RegisterReviveLight(this.gameObject);
        this.gameObject.SetActive(false);
    }

    //활성화할때, 이펙트 코루틴 동작
    private void OnEnable()
    {
        StartCoroutine(ReviveEffect());
    }

    /// <summary>
    /// Player와 효과의 위치를 동기화
    /// </summary>
    private void FixedUpdate()
    {
        Vector3 playerXZ = player.transform.position;
        playerXZ.y = this.transform.position.y;

        this.transform.position = playerXZ;
    }

    /// <summary>
    /// 효과 종료 체크
    /// </summary>
    private void Update()
    {
        if (effectEnd)
        {
            player.GetComponent<Player>().OffRevive();
            gameObject.SetActive(false);
        }
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// 효과가 시작할땐, 빛이 퍼지고,
    /// 깜빡거리며 사라져 효과가 끝남을 알림
    /// </summary>
    IEnumerator ReviveEffect()
    {
        player = GlobalData.Instance.Player;

        float duration = 0f;

        while (duration < effectTimer)
        {
            duration += Time.deltaTime;

            float ratio = duration / effectTimer;

            // 선형 증가를 위한 비율과 각도 측정
            float fianlIntensity = Mathf.Lerp(0, targetIntensity, ratio);
            float finalAngle = Mathf.Lerp(0, targetAngle, ratio);

            targetLight.intensity = fianlIntensity;
            targetLight.spotAngle = finalAngle;
            yield return null;
        }

        duration = 0f;

        while (duration < offTimer)
        {
            duration += Time.deltaTime;

            // 0 ~ 1 사이 왕복 (깜빡임 비율)
            float blinkRatio = Mathf.PingPong(duration * pingpongSpeed, 1f);

            // targetIntensity의 50% ~ 100% 사이 깜빡임
            targetLight.intensity = targetIntensity * (0.5f + blinkRatio * 0.5f);
            yield return null;
        }

        effectEnd = true;
    }

    #endregion
}
