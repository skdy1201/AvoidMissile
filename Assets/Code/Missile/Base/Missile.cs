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
    [SerializeField] private int spawnTime;

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

    public int SpawnTime
    {
        get => spawnTime;
        set => spawnTime = value;
    }

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

    #region Public Methods

    /// <summary>
    /// 충돌 지점의 폭발 효과를 동작시키기 위한 함수
    /// </summary>
    /// <param name="targetPosition"> 충돌한 지점 </param>
    public void ActiveBombEffect(Vector3 targetPosition)
    {
        // 폭발 이펙트 빌려와서 동작
        GameObject boomEffect = BoomEffectSpawner.Instance.RentSpawner(BoomParticle.Normal);

        boomEffect.transform.position = targetPosition;
        boomEffect.SetActive(true);

        AudioController.Instance.PlayExploreSound();
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// PowerJump 상태의 Player와 충돌했을때, 미사일은 파괴
    /// </summary>
    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            Player player = collision.gameObject.GetComponent<Player>();

            if(player.ActivePowerJump)
            {
                this.gameObject.SetActive(false);
                MissileSpawner.Instance.ReturnSpawner(MissileType.YAxis ,this.gameObject);

                GameProgress.Instance.Score = GameProgress.Instance.Score;

                // 폭발 이펙트를 정확한 충돌 위치에 표시하기 위해 접점 저장
                Vector3 contact = collision.contacts[0].point;

                ActiveBombEffect(contact);
            }
        }
    }

    #endregion

}
