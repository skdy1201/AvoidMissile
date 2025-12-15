using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


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

    private List<bool> itemSpawnTies = Enumerable.Repeat(false, 100).ToList();

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

        // 에디터에서의 레이어 설정이 이상해, 코드로 직접 설정
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
        Item itemcomponent = item.GetComponent<Item>();

        // 아이템 스폰 체크 리스트 갱신
        if(itemcomponent != null)
        {
            int spawnidx = itemcomponent.SpawnTile;

            if (spawnidx > -1)
                itemSpawnTies[spawnidx] = false;
        }
        
        if(activeItems.Contains(item))
            activeItems.Remove(item);

        Debug.Log("in here");

        // item check list 갱신
        OffSpawnTileidx(itemcomponent.SpawnTile, itemcomponent);

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

    // 아이템.cs에서 off 해줄 수 있도록 하는 public 함수
    public void OffSpawnTileidx(int tileidx, Item item)
    {
        Debug.Log($"tileidx is {tileidx}");

        if (itemSpawnTies[tileidx] == true)
            itemSpawnTies[tileidx] = false;

        item.SpawnTile = -1;
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
        while (true)
        {
            int spawnTile = Random.Range(1, maxItemSpawn);

            for(int i = 0; i < spawnTile; ++i)
            {
                int randomrange = Random.Range(0, 99);

                // 3번 다시 찾는데 중복이라면 그냥 넘어가기
                if (itemSpawnTies[randomrange] == true)
                {
                    int count = 3;

                    while (itemSpawnTies[randomrange] == true && count > 0)
                    {
                        randomrange = Random.Range(0, 99);
                        --count;
                    }

                    continue;
                }

                // 타일 체크
                itemSpawnTies[randomrange] = true;

                Debug.Log($"tile idx = {randomrange}");

                int itemType = Random.Range(0, System.Enum.GetValues(typeof(ItemType)).Length);

                // 스폰 위치 재조정
                Vector3 tilePos = gamePlatform.GetTile(randomrange).transform.position;
                tilePos.y += 1.5f;
                tilePos.x -= GlobalData.Instance.TileXScale / 2f;
                tilePos.z += GlobalData.Instance.TileZScale / 2f;
                GameObject item = RentSpawner((ItemType)itemType);

                item.transform.position = tilePos;

                item.GetComponent<Item>().SpawnTile = spawnTile;

                activeItems.AddLast(item);

                item.SetActive(true);
            }

           
            yield return new WaitForSeconds(10f);
        }

    }

    #endregion

}
