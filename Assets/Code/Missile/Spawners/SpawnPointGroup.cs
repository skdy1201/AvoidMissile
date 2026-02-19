using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 8방향 스폰 포인트를 관리하는 컴포넌트 (직선 4 + 대각선 4)
/// </summary>
public class SpawnPointGroup : MonoBehaviour
{
    #region Private/Protected Fields

    private List<GameObject> northPoints = new List<GameObject>();
    private List<GameObject> southPoints = new List<GameObject>();
    private List<GameObject> eastPoints = new List<GameObject>();
    private List<GameObject> westPoints = new List<GameObject>();

    private List<GameObject> nePoints = new List<GameObject>();
    private List<GameObject> nwPoints = new List<GameObject>();
    private List<GameObject> sePoints = new List<GameObject>();
    private List<GameObject> swPoints = new List<GameObject>();

    #endregion

    #region Properties

    public List<GameObject> NorthPoints => northPoints;
    public List<GameObject> SouthPoints => southPoints;
    public List<GameObject> EastPoints => eastPoints;
    public List<GameObject> WestPoints => westPoints;

    public List<GameObject> NEPoints => nePoints;
    public List<GameObject> NWPoints => nwPoints;
    public List<GameObject> SEPoints => sePoints;
    public List<GameObject> SWPoints => swPoints;

    #endregion

    #region Public Method

    /// <summary>
    /// 스폰 포인트를 얻어오는 함수
    /// </summary>
    /// <param name="direction"> 0=북, 1=남, 2=동, 3=서, 4=NE, 5=NW, 6=SE, 7=SW </param>
    /// <param name="index"> 해당 축의 인덱스 </param>
    public Vector3 GetPointPosition(int direction, int index)
    {
        return direction switch
        {
            0 => northPoints[index].transform.position,
            1 => southPoints[index].transform.position,
            2 => eastPoints[index].transform.position,
            3 => westPoints[index].transform.position,
            4 => nePoints[index].transform.position,
            5 => nwPoints[index].transform.position,
            6 => sePoints[index].transform.position,
            7 => swPoints[index].transform.position,
            _ => throw new System.ArgumentOutOfRangeException(nameof(direction), direction, "direction은 0~7이어야 합니다.")
        };
    }

    public Vector3 GetRandomPoint()
    {
        int pointsPerDirection = northPoints.Count;
        int totalPoints = pointsPerDirection * 4;
        int index = Random.Range(0, totalPoints);

        return GetPointPosition(index / pointsPerDirection, index % pointsPerDirection);
    }

    #endregion
}
