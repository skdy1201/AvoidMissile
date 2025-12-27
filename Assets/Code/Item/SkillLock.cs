using UnityEngine;

/// <summary>
/// 스킬 사용을 금지시키는 아이템
/// </summary>
public class SkillLock : Item
{
    public override void EffectItem()
    {
        Debug.Log("in Skill Lock Item");

        Player player = GlobalData.Instance.Player.GetComponent<Player>();

        if (player.GetComponent<Player>() != null)
        {
            player.SkillLock(time);

            AudioController.Instance.PlayItemSound((int)ItemEffectSound.Lock);

            GameProgress.Instance.AddCustomScore(25);

        }
    }
}
