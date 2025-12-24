using UnityEngine;

/// <summary>
/// 스킬 사용을 금지시키는 아이템
/// </summary>
public class SkillLock : Item
{
    public override void EffectItem()
    {
        Player player = GlobalData.Instance.Player.GetComponent<Player>();

        if (player.GetComponent<Player>() != null)
        {
            player.SkillLock(time);

            AudioController.Instance.PlayItemSound((int)ItemEffectSound.Lock);
        }
    }
}
