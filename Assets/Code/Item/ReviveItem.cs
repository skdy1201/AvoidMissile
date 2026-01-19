using UnityEngine;
using UnityEngine.Events;

public class ReviveItem : Item
{
    /// <summary>
    /// 부활은 게임 당 한번 만 작동하게 하기 위해, 아이템을 먹으면 확률을 재조정
    /// </summary>
    public override void EffectItem()
    {
        Debug.Log("Take Revive");

        if(GameProgress.Instance.PlayerAlive == false)
        {
            AudioController.Instance.PlayItemSound((int)ItemEffectSFX.Revive);

            ItemSpawner.Instance.TakeRevive();

            // 부활여부 체크 갱신
            GlobalData.Instance.Player.GetComponent<Player>().Revive = true;
        }
    }

}
