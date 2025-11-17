using UnityEngine;

/// <summary>
/// 충돌한 객체가 플레이어라면, 사망이벤트 동작
/// </summary>
public class CheckGameOver : MonoBehaviour
{

    #region Unity Lifecycle
    /// <summary>
    /// 미사일과 플레이어의 충돌을 감지하여 게임 오버를 처리
    /// </summary>
    /// <param name="collision"> 충돌 정보 </param>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            Player.OnPlayerDead?.Invoke();
        }
    }
    #endregion

}
