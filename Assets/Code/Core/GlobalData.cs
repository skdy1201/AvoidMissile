using UnityEngine;

/// <summary>
/// 전반적으로 공유되면 좋을 데이터들을 관리하는 스크립트
/// </summary>
/// <remarks>
/// 씬 전환 관리, 타일/플레이어 참조, 게임 진행 관련 상수를 제공
/// 씬이 전환되어도 DontDestroyOnLoad로 유지
/// </remarks>
public class GlobalData : Singleton<GlobalData>
{
    #region Serialized Fields


    [SerializeField] private GameObject player;

    [SerializeField] private GameObject tilePrefab;

    [SerializeField] private float missileDropPoint;

    [SerializeField] private Vector3 tileSize;

    [SerializeField] private string prevSceneStr;

    #endregion

    #region Private/Protected Fields

    private static string PlaySceneStr = "PlayScene";

    private static string TitleSceneStr = "TitleScene";

    private static string DevTestSceneStr = "DevTestScene";

    #endregion

    #region Properties
    public float TileXScale => tileSize.x;
    public float TileYScale => tileSize.y;
    public float TileZScale => tileSize.z;

    public float MissileDropPoint => missileDropPoint;

    public string PrevScene => prevSceneStr;
    public string TitleScene => TitleSceneStr;
    public string PlayScene => PlaySceneStr;

    public string DevTestScene => DevTestSceneStr;

    public GameObject Player
    {
        get { return player; }
        set { player = value; }
    }

    public GameObject TilePrefab
    {
        get { return tilePrefab; }
        set { tilePrefab = value; }
    }
    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        GameProgress.EndScene.AddListener(EndProtocol);
      
        tileSize = tilePrefab.GetComponent<MeshFilter>().sharedMesh.bounds.size;

        prevSceneStr = TitleSceneStr;
    }

    #endregion

    #region Private/Protected Methods

    protected override void StartProtocol()
    {
    }

    /// <summary>
    /// 해당 씬의 플레이어는 죽었으니 연결을 해제.
    /// </summary>
    protected override void EndProtocol()
    {
        player = null;
    }
    #endregion


}
