using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// GameOver 시, 그레이 스케일을 동작
/// </summary>
public class GreyScaleTrigger : MonoBehaviour
{
    #region Private/Protected Fields

    private Volume volumeComponent;


    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 볼륨 스크립트 매칭.
    /// 이벤트 등록
    /// </summary>
    private void Awake()
    {
        volumeComponent = GetComponent<Volume>();

        if (volumeComponent == null)
            Debug.LogError("Can't match Volume");

        volumeComponent.enabled = false;

        Player.OnPlayerDead.AddListener(() => StartCoroutine(StartGrayScale()));

        GameProgress.Instance.RegisterGreyScaleTrigger(this);
    }

    private void OnDestroy()
    {
        Player.OnPlayerDead.RemoveListener(() => StartGrayScale());
    }

    #endregion

    #region Public Method
    
    /// <summary>
    /// RewardAd 이후 다시 플레이 하기 위해 볼륨 컴포넌트 비활성화 및 초기화
    /// </summary>
    public void ResetGreyScale()
    {
        volumeComponent.weight = 0f;
        volumeComponent.enabled = false;
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// 1초 동안 가중치를 쌓아가며 그레이 스케일을 만든다
    /// </summary>
    private IEnumerator StartGrayScale()
    {
        volumeComponent.enabled = true;
        volumeComponent.weight = 0f;

        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            float smoothT = Mathf.SmoothStep(0, 1, t);
            volumeComponent.weight = smoothT;

            yield return null;
        }

        volumeComponent.weight = 1f; // 최종값 보장

    }

    #endregion

}
