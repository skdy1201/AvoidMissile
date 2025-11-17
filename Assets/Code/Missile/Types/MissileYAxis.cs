using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Y축 미사일 스크립트
/// </summary>
public class MissileYAxis : Missile
{
    #region Serialized Fields
    
    [SerializeField] public Vector2 XZCoord = new Vector2();

    #endregion

    #region Private/Protected Fields

    private Material missileMaterial = null;

    #endregion

    #region Properties

    public bool MissileTransparent { get; set; } = false;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 콜라이더 컴포넌트 초기화. 충돌 감지를 위해 Awake에서 캐싱
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        missileCollider = GetComponent<Collider>();

    }

    #endregion

    #region Public Methods

    public float GetMissileAlpha() => missileMaterial.color.a;

    /// <summary>
    /// 미사일의 재질을 바꿈
    /// </summary>
    /// <param name="transparent"> 인자로 들어온 재질</param>
    public void ChangeMaterial(Material transparent)
    {
        MeshRenderer missileRenderer = this.gameObject.GetComponent<MeshRenderer>();

        missileRenderer.material = transparent;
        missileMaterial = transparent;

        MissileTransparent = true;
    }

   /// <summary>
   /// 알파 값 변경 함수
   /// </summary>
   /// <param name="alphaValue"> 변경할 알파 값</param>
    public void ChangeAlpha(float alphaValue)
    {
        // 재질 연결을 아직 하지 않았으므로,
        if (missileMaterial == null)
            return;

        missileMaterial.color = new Color(missileMaterial.color.r, missileMaterial.color.g, missileMaterial.color.b, alphaValue);
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// Y축 미사일 충돌
    /// </summary>
    /// <param name="collision"> 충돌한 오브젝트 콜라이더 </param>
    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        // 폭발 이펙트를 정확한 충돌 위치에 표시하기 위해 접점 저장
        Vector3 contact = collision.contacts[0].point;

        // 폭발 이펙트 받아두기
        // TODO: 미사일 이펙트가 폭파하지 않는다면 봐야할 부분
        GameObject boomEffect = BoomEffectSpawner.Instance.RentSpawner(BoomParticle.Normal);

        boomEffect.transform.position = contact;
        boomEffect.SetActive(true);

        AudioController.Instance.PlayExploreSound();

        // 충돌체의 레이어에 따른 조치
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            this.CollisionOther = true;

            this.gameObject.SetActive(false);
            MissileSpawner.Instance.ReserveReturn(this.gameObject);

            GameProgress.Instance.Score = GameProgress.Instance.Score;
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("Missile"))
        {
            // Y축 미사일과 충돌한다면, 두 미사일의 Y 값에 따라서 계속 떨어질지, 없어질지 결정 
            // 아래에 있는 미사일은 더 가속을 받고 떨어지며, 위에 있는 미사일은 풀로 돌아간다.
            // 혹시나 Y축이 순간적으로 가까운 경우가 있다면 인스턴스 ID를 통한 비교
            if (collision.gameObject.GetComponent<MissileYAxis>() != null)
            {
                float otherMissileY = collision.gameObject.transform.position.y;

                if (this.gameObject.transform.position.y > otherMissileY)
                {
                    // 미사일 속도 조정
                    Rigidbody lowerRigidBody = collision.gameObject.GetComponent<Rigidbody>();
                    lowerRigidBody.linearDamping = Mathf.Clamp(lowerRigidBody.linearDamping, 1.5f, lowerRigidBody.linearDamping - 0.05f);

                    this.gameObject.GetComponent<Missile>().CollisionOther = true;

                    // 이 미사일은 풀로 반환
                    collision.gameObject.SetActive(false);
                    MissileSpawner.Instance.ReserveReturn(this.gameObject);
                }
                else if (this.gameObject.transform.position.y == otherMissileY)
                {
                    //좌표가 같다면  인스턴스 ID로 비교
                    if (this.gameObject.GetInstanceID() > collision.gameObject.GetInstanceID())
                    {
                        this.gameObject.GetComponent<Missile>().CollisionOther = true;

                        collision.gameObject.SetActive(false);
                        MissileSpawner.Instance.ReserveReturn(this.gameObject);
                    }
                }
            }
        }
    }

    #endregion
 
}
