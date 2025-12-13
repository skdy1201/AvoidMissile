using UnityEngine;
using System.Collections;
using System.Collections.Generic;


/// <summary>
/// 아이템 타입 열거형
/// </summary>
public enum ItemType
{
    SpeedUp,
    SpeedDown,
    Control,
    Slide,
    //GameOver,
};

/// <summary>
/// 아이템 관련 스포너
/// </summary>
/// <remarks>
/// 아이템들을 관리 및 게임 내 스폰을 담당
/// </remarks>
public class ItemSpawner : Spawner<ItemType>
{
    #region Serialized Fields

    // 아이템들의 프리팹을 관리
    [SerializeField] List<GameObject> ItemPrefabs;
    
    // 타일들을 랜덤으로 추출하고 스폰하기 위한 플랫폼
    [SerializeField] private Platform gamePlatform;

    [SerializeField] private int maxItemSpawn = 4;

    [SerializeField] private LinkedList<GameObject> activeItems = new LinkedList<GameObject>();
    #endregion

    #region Private/Protected Fields

    // DevTestScene에서 테스트 할 때, 한 번만 동작시키기 위한 변수
    private bool DevTestActive = false;

    #endregion

    #region Properties

    public new static ItemSpawner Instance
    {
        get { return Singleton<Spawner<ItemType>>.Instance as ItemSpawner; }
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 사망 이벤트 등록 및 아이템 풀 초기화
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        Player.OnPlayerDead.AddListener(OnPlayerDeath);

        // 아이템 프리팹들을 하나씩 생성해 스포너에 집어넣기
        int itemcount = System.Enum.GetValues(typeof(ItemType)).Length;

        for (int i = 0; i < itemcount; ++i)
        {
            GameObject item = Instantiate(ItemPrefabs[i]);
            item.transform.parent = this.transform;
            item.SetActive(false);
            spawners[i].Enqueue(item);
        }

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("GameItem"), LayerMask.NameToLayer("Missile"), true);
    }

    // Update is called once per frame
    void Update()
    {
    
        // DevTest Scene에서 테스트
        #if UNITY_EDITOR
        if (Input.GetKey(KeyCode.I) && DevTestActive == false)
        {
            DevTestActive = true;
            StartCoroutine(ItemSpawnLoop());
        }
        #endif
    }

#endregion

    #region Public Methods

    // item Type에 따라, Spawner를 확인해보고, 있으면 반환 없으면 생성
    public override GameObject RentSpawner(ItemType type)
    {
        GameObject item = null;

        int spawnerIndex = (int)type;

        if (spawners[spawnerIndex].Count <= 0)
        {
            item = Instantiate(ItemPrefabs[spawnerIndex]);
            return item;
        }

        item = spawners[spawnerIndex].Dequeue();

        item.transform.parent = null;

        return item;
    }

    // 다시 큐에 되돌려놓기
    public override void ReturnSpawner(ItemType type, GameObject item)
    {
        if(activeItems.Contains(item))
            activeItems.Remove(item);

        int spawnerIndex = (int)type;
        item.SetActive(false);
        item.transform.parent = this.transform;
        spawners[spawnerIndex].Enqueue(item);
    }

    // 플랫폼 등록 함수
    public void SetPlatform(Platform platform)
    {
        gamePlatform = platform;
    }

    #endregion

    #region Private/Protected Methods

    protected override void StartProtocol()
    {

    }

    protected override void EndProtocol()
    {
        gamePlatform = null;

        if(activeItems.Count > 0)
        {
            while(activeItems.Count > 0)
            {
                var curitem = activeItems.First;

                GameObject gameObject = curitem.Value;

                if ((gameObject.GetComponent<Item>() != null))
                {
                    ReturnSpawner(gameObject.GetComponent<Item>().Category, gameObject);
                }
            }
        }
    }

    #endregion

    #region Event Handlers
    #endregion

    #region Coroutines

    /// <summary>
    /// 아이템 스폰 루프
    /// </summary>
    IEnumerator ItemSpawnLoop()
    {
        // 중복 체크 set
        HashSet<int> spawnTileCheck = new HashSet<int>();

        while (true)
        {
            int spawnTile = Random.Range(1, maxItemSpawn);

            for(int i = 0; i < spawnTile; ++i)
            {
                int randomrange = Random.Range(0, 99);

                // 이미 선택된 타일이면, 한 차례 건너 뛴다.
                if (spawnTileCheck.Contains(randomrange))
                    continue;

                spawnTileCheck.Add(randomrange);

                Debug.Log($"tile idx = {randomrange}");

                int itemType = Random.Range(0, System.Enum.GetValues(typeof(ItemType)).Length);

                // 스폰 위치 재조정
                Vector3 tilePos = gamePlatform.GetTile(randomrange).transform.position;
                tilePos.y += 1.5f;
                tilePos.x -= GlobalData.Instance.TileXScale / 2f;
                tilePos.z += GlobalData.Instance.TileZScale / 2f;
                GameObject item = RentSpawner((ItemType)itemType);

                item.transform.position = tilePos;

                activeItems.AddLast(item);

                item.SetActive(true);
            }

            // 스폰 끝났으니 초기화
            spawnTileCheck.Clear();
           
            yield return new WaitForSeconds(10f);
        }

    }

    #endregion

}
