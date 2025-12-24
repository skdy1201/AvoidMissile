using UnityEngine;

/// <summary>
/// 플레이어의 조작을 반전시키는 디버프 아이템
/// </summary>
public class ControlReverse : Item
{
    /// <summary>
    /// 지정된 시간동안 입력값을 반전
    /// </summary>
    public override void EffectItem()
    {
        GameObject player = GlobalData.Instance.Player;

        if (player.GetComponent<Player>() != null)
        {
            player.GetComponent<Player>().ControlReverse(this.time);

            AudioController.Instance.PlayItemSound((int)ItemEffectSound.Reverse);
        }
    }
}
