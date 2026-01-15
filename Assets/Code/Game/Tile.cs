using UnityEngine;

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
        // 타일 사이즈 체크
        worldSize = Vector3.Scale(this.gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.size, transform.lossyScale);

        float tilex = worldSize.x;
        float tileZ = worldSize.z;
        float tileY = worldSize.y;
    }

    #endregion

}
