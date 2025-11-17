using System.Collections;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 미사일 추적 및 경고 데칼 표시를 담당하는 타일 스크립트
/// </summary>
public class Tile : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private int ID;

    [FormerlySerializedAs("MissileNum")]
    [SerializeField] private int missileNum = -1;

    [SerializeField] GameObject warningDecal;

    #endregion

    #region Private/Protected Fields

    private bool collision = false;
    private int currentMissileNum = -1;

    private Collider tileCollider;

    private Transform warningDecalTransform;

    #endregion

    #region Properties

    public int TileID
    {
        get { return ID; }
        set { ID = value; }
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 데칼 사이즈를 0으로 변경
    /// </summary>
    void Start()
    {
        warningDecalTransform = warningDecal.transform;
        warningDecal.GetComponent<Transform>().localScale = Vector3.zero;
        tileCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// 타일 위로 떨어지는 미사일을 추적
    /// </summary>
    void FixedUpdate()
    {
        RayCastMissile();
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// Y축 미사일과 충돌을 하면 충돌을 확인하는 bool 변수를 체크
    /// </summary>
    /// <param name="collision"></param>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<MissileYAxis>() != null)
        {
            this.collision = true;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void RayCastMissile()
    {
        // 레이캐스트 시작 위치
        Vector3 raycastPos = this.transform.position;

        // 타일 사이즈 체크
        Vector3 worldSize = Vector3.Scale(this.gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.size, transform.lossyScale);
        float tilex = worldSize.x;
        float tileZ = worldSize.z;
        float tileY = worldSize.y;

        // 레이캐스트를 타일의 중앙으로 맞추기
        raycastPos.x -= tilex / 2;
        raycastPos.z += tileZ / 2;
        raycastPos.y += tileY;

        // Missile 레이어만 충돌 체크
        RaycastHit[] missilehit;

        int layerMask = 1 << LayerMask.NameToLayer("Missile");  

        missilehit = Physics.RaycastAll(raycastPos, Vector3.up, GlobalData.Instance.MissileDropPoint + Mathf.Abs(this.transform.position.y), layerMask);

        // 충돌 미사일이 있으면 찾고, 없으면 데칼 비활성화
        if (missilehit.Length > 0)
        {
            // 가장 낮은 높이의 미사일 찾기
            bool foundYAxisMissile = false;
            int yAxisIndex = -1;
            float lowestheight = 9999f;

            for (int i = 0; i < missilehit.Length; ++i) 
            {
                if (missilehit[i].collider.gameObject.GetComponent<MissileYAxis>() != null)
                {
                    foundYAxisMissile = true;
                    if (lowestheight > missilehit[i].collider.transform.position.y)
                    {
                        lowestheight = missilehit[i].collider.transform.position.y;
                        yAxisIndex = i;
                        currentMissileNum = missilehit[i].collider.gameObject.GetComponent<Missile>().MissileNumber;
                    }
                }
            }

            // 가장 낮은 높이 미사일을 찾으면, 데칼 비율 조정
            if (foundYAxisMissile) 
            {
                float missileheight = missilehit[yAxisIndex].collider.gameObject.transform.position.y + Mathf.Abs(this.gameObject.transform.position.y);
                float ratio = 100 - (missileheight / (Mathf.Abs(this.gameObject.transform.position.y) + GlobalData.Instance.MissileDropPoint) * 100);

                if (ratio < 0 || ratio > 100)
                {
                    Debug.LogError($"[Tile {TileID}] NEGATIVE RATIO! Missile too high?"
                        + $"Cur Ratio is : {ratio}");

                }

                float scalevalue = ratio * (1f / 100f);
                scalevalue = Mathf.Floor(scalevalue * 100f) / 100f;
                scalevalue = Mathf.Clamp(scalevalue, 0, 1);
                warningDecal.GetComponent<Transform>().localScale = Vector3.one * scalevalue;


                if (collision)
                    collision = false;
            }
            else
            {
                // Y축 미사일이 없으면 데칼 제거
                warningDecalTransform.localScale = Vector3.zero;
            }

        }
        else
            warningDecalTransform .localScale = Vector3.zero;

    }

    #endregion

}
