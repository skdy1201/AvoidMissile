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

    [SerializeField] private int missileNum = -1;

    [SerializeField] Vector3 worldSize; 

    #endregion

    #region Private/Protected Fields

    private bool collision = false;
    private int currentMissileNum = -1;

    private Transform warningDecalTransform;

    private RaycastHit[] raycastBuffer = new RaycastHit[50];

    private Vector3 raycastPos = Vector3.zero;

    private int layerMask; 
    private Ray decalRay = new Ray();

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
        // 데칼 위치, 크기 세팅
      // warningDecalTransform = warningDecal.transform;
      // warningDecal.GetComponent<Transform>().localScale = Vector3.zero;

        // 레이캐스트 시작 위치
        raycastPos = this.transform.position;

        // 타일 사이즈 체크
        worldSize = Vector3.Scale(this.gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.size, transform.lossyScale);

        float tilex = worldSize.x;
        float tileZ = worldSize.z;
        float tileY = worldSize.y;

        // 레이캐스트를 타일의 중앙으로 맞추기
        raycastPos.x -= tilex / 2;
        raycastPos.z += tileZ / 2;
        raycastPos.y += tileY;

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
    /// 
    /// </summary>
    private void RayCastMissile()
    {
       //// Missile 레이어만 충돌 체크
       //int hitPoint = Physics.RaycastNonAlloc(decalRay, raycastBuffer, GlobalData.Instance.MissileDropPoint + Mathf.Abs(this.transform.position.y), layerMask, QueryTriggerInteraction.Collide);
       //
       //// 충돌 미사일이 있으면 찾고, 없으면 데칼 비활성화
       //if (hitPoint > 0)
       //{
       //    // 가장 낮은 높이의 미사일 찾기
       //    bool foundYAxisMissile = false;
       //    int yAxisIndex = -1;
       //    float lowestheight = 9999f;
       //
       //    for (int i = 0; i < raycastBuffer.Length; ++i) 
       //    {
       //        if (raycastBuffer[i].collider.gameObject.GetComponent<MissileYAxis>() != null)
       //        {
       //            foundYAxisMissile = true;
       //            if (lowestheight > raycastBuffer[i].collider.transform.position.y)
       //            {
       //                lowestheight = raycastBuffer[i].collider.transform.position.y;
       //                yAxisIndex = i;
       //                currentMissileNum = raycastBuffer[i].collider.gameObject.GetComponent<Missile>().MissileNumber;
       //            }
       //        }
       //    }
       //
       //    // 가장 낮은 높이 미사일을 찾으면, 데칼 비율 조정
       //    if (foundYAxisMissile) 
       //    {
       //        float missileheight = raycastBuffer[yAxisIndex].collider.gameObject.transform.position.y + Mathf.Abs(this.gameObject.transform.position.y);
       //        float ratio = 100 - (missileheight / (Mathf.Abs(this.gameObject.transform.position.y) + GlobalData.Instance.MissileDropPoint) * 100);
       //
       //        if (ratio < 0 || ratio > 100)
       //        {
       //            Debug.LogError($"[Tile {TileID}] NEGATIVE RATIO! Missile too high?"
       //                + $"Cur Ratio is : {ratio}");
       //
       //        }
       //
       //        float scalevalue = ratio * (1f / 100f);
       //        scalevalue = Mathf.Floor(scalevalue * 100f) / 100f;
       //        scalevalue = Mathf.Clamp(scalevalue, 0, 1);
       //       // warningDecal.GetComponent<Transform>().localScale = Vector3.one * scalevalue;
       //
       //
       //        if (collision)
       //            collision = false;
       //    }
       //    else
       //    {
       //        // Y축 미사일이 없으면 데칼 제거
       //        warningDecalTransform.localScale = Vector3.zero;
       //    }
       //
       //}
       //else
       //    warningDecalTransform .localScale = Vector3.zero;

    }

    #endregion

}
