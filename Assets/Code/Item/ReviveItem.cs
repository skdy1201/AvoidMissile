using UnityEngine;

public class ReviveItem : Item
{
    /// <summary>
    /// 부활은 게임 당 한번 만 작동하게 하기 위해, 아이템을 먹으면 확률을 재조정
    /// </summary>
    public override void EffectItem()
    {
        ItemSpawner.Instance.TakeRevive();
        ItemSpawner.Instance.TestPercent();
    }
}
