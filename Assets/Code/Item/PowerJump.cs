using UnityEngine;

public class PowerJump : Item
{
    #region Public Methods

    /// <summary>
    /// Player의 Slide 능력을 바꾼다.
    /// </summary>
    /// <remarks>
    /// 미사일도 파괴 가능
    /// </remarks>
    public override void EffectItem()
    {
        GameObject player = GlobalData.Instance.Player;

        if (player.GetComponent<Player>() != null)
        {
            player.GetComponent<Player>().ReinforceSlide(this.time);
        }
    }

    #endregion
}
