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
    #region Private/Protected Fields

    private Collider itemCollider = null;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 콜라이더를 미리 캐싱
    /// </summary>
    private void Awake()
    {
        itemCollider = GetComponent<Collider>();

        if (itemCollider == null)
            Debug.LogError("item doesn't have collider");
    }

    #endregion

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
        }
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 플레이어와 충돌한다면, 아이템의 효과를 발동시키고, 스포너에 반환한다.
    /// </summary>
    /// <param name="otherCollider"> 충돌한 다른 오브젝트 </param>
    private void OnCollisionEnter(Collision otherCollider)
    {
        if (otherCollider.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            EffectItem();
            ItemSpawner.Instance.ReturnSpawner(ItemType.SpeedUp, this.gameObject);
        }
    }

    #endregion

}
