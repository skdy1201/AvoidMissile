using Unity.IntegerTime;
using UnityEngine;

/// <summary>
/// Missile들의 최상위 스크립트
/// </summary>
/// <remarks>
/// 미사일 콜라이더
/// </remarks>
public class Missile : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private int missileNumber;
    [SerializeField] private bool missileReturn;

    #endregion

    #region Private/Protected Fields

    protected Collider missileCollider;

    #endregion

    #region Properties

    public int MissileNumber 
    { 
        get => missileNumber; 
        set => missileNumber = value;
    }

    public bool CollisionOther { get; set; } = false;

    public bool MissileReturn { get => missileReturn; set => missileReturn = value; }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 충돌 레이어 설정
    /// </summary>
    protected virtual void Awake()
    {
        this.gameObject.layer = LayerMask.NameToLayer("Missile");
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 추후 구현사항이 생길 수 있어, 지우지 않고 남겨둠
    /// </summary>
    protected virtual void OnCollisionEnter(Collision collision) { }

    #endregion

}
