using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 플랫폼을 생성하고 관리하는 클래스
/// 지정된 크기의 타일 그리드를 생성하고 각 타일에 고유 ID를 부여
/// </summary>
public class Platform : MonoBehaviour
{
    #region Serialized Fields

    [FormerlySerializedAs("PlatformTile")]
    [SerializeField] private GameObject platformTile;

    /// <summary>
    /// 플랫폼의 크기 (x: 열 개수, y: 행 개수)
    /// </summary>
    [SerializeField] private Vector2 platformSize;

    #endregion

    #region Private/Protected Fields

    /// <summary>
    /// 플랫폼에 생성된 모든 타일 목록
    /// </summary>
    private List<GameObject> tiles = new List<GameObject>();
    
    private float tileXsize;
    private float tileZsize;

    #endregion

    #region Properties

    public int TileSize => tiles.Count - 1;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        int platformCol = (int)platformSize.x;
        int platformRow = (int)platformSize.y;

        // 타일 사이즈 받아두기
        tileXsize = platformTile.GetComponent<MeshCollider>().sharedMesh.bounds.size.x;
        tileZsize = platformTile.GetComponent<MeshCollider>().sharedMesh.bounds.size.z;

        Vector3 currentPosition = this.transform.position;

        // 타일들을 하나 씩 인스턴스 하며, 설정
        for (int i = 0; i < platformCol; ++i)
        {
            for (int j = 0; j < platformRow; ++j)
            {
                Vector3 spawnPos = currentPosition;
                spawnPos.x += tileXsize * j;
                spawnPos.z -= tileZsize * i;

                GameObject tile = Instantiate(platformTile, spawnPos, Quaternion.identity);
                tile.layer = LayerMask.NameToLayer("Platform");
                tile.transform.parent = this.transform;

                Tile tilecomponent = tile.GetComponent<Tile>();
                tilecomponent.TileID = (i * platformCol + j);

                tiles.Add(tile);
            }
        }

        // 미사일 스포너에 플랫폼 등록
        MissileSpawner.Instance.gamePlatform = this;
        
        ItemSpawner.Instance.SetPlatform(this);
       
    }

    #endregion

    #region Public Methods
    public GameObject GetTile(int tileID) => tiles[tileID];

    #endregion

}
