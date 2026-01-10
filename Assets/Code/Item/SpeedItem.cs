using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Player의 이동속도를 조정하는 아이템
/// </summary>
/// <remakrs>
/// 버프, 너프 상관 없이 사용할 수 있도록 수치를 통해 구별
/// 각 이동속도 관련 아이템은 해당 스크립트를 가짐
/// </remakrs>
public class MoveFast : Item
{
    #region Public Methods

    /// <summary>
    /// Player의 속도를 조절
    /// </summary>
    public override void EffectItem()
    {
        GameObject player = GlobalData.Instance.Player;

        if (player.GetComponent<Player>() != null)
        {
            player.GetComponent<Player>().ChangeSpeed(this.value, this.time);

            if (category == ItemType.SpeedUp)
                AudioController.Instance.PlayItemSound((int)ItemEffectSFX.Buff);
            else
            {
                AudioController.Instance.PlayItemSound((int)(ItemEffectSFX.Nerf));
                GameProgress.Instance.AddCustomScore(5);
            }
        }
    }

    #endregion

}
