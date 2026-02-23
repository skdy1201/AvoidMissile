using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 아이템 데이터 직렬화 구조체
/// </summary>
public struct ItemData
{
    public string Name;
    public string Percent;
    public string Value;
    public string Time;
}

/// <summary>
/// 아이템 타입 열거형
/// </summary>
public enum ItemType
{
    SpeedUp,
    SpeedDown,
    Control,
    Slide,
    Lock,
    Revive,
};

/// <summary>
/// 아이템 생성 매니저
/// </summary>
/// <remarks>
/// 아이템들의 생성 및 관리 및 반환을 담당
/// </remarks>
public class ItemSpawner : Spawner<ItemType>
{
    #region Serialized Fields

    // 아이템들을 프리팹으로 관리
    [SerializeField] List<GameObject> ItemPrefabs;

    // 타일들의 좌표값을 참조하고 사용하기 위한 플랫폼
    [SerializeField] private Platform gamePlatform;

    [SerializeField] private int maxItemSpawn = 4;

    [SerializeField] private LinkedList<GameObject> activeItems = new LinkedList<GameObject>();

    [SerializeField] private List<int> ItemPercent = new List<int>();

    [SerializeField] private List<int> itemNamingNumber = Enumerable.Repeat(0, System.Enum.GetValues(typeof(ItemType)).Length).ToList();

    [Header("Test")]
    [SerializeField] private bool spawnRevive = false;
    #endregion

    #region Private/Protected Fields

    private List<bool> itemSpawnTies = Enumerable.Repeat(false, 100).ToList();

    // 부활 아이템을 먹었는지 체크
    private bool activeRevive = false;

    #endregion

    #region Properties

    public new static ItemSpawner Instance
    {
        get { return Singleton<Spawner<ItemType>>.Instance as ItemSpawner; }
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 사망 이벤트 등록 및 오브젝트 풀 초기화
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        Player.OnPlayerDead.AddListener(OnPlayerDeath);

        SyncItemData();

        // 아이템 오브젝트들을 하나씩 오브젝트 스포너에 넣어두기
        int itemcount = System.Enum.GetValues(typeof(ItemType)).Length;

        Debug.Log($"item count is {itemcount}");
        Debug.Log($"itemNamingNUmber is {itemNamingNumber.Count}");
        for (int i = 0; i < itemcount; ++i)
        {
            GameObject item = Instantiate(ItemPrefabs[i]);
            item.transform.parent = this.transform;

            item.name = ItemPrefabs[i].name + itemNamingNumber[i];
            itemNamingNumber[i]++;

            item.SetActive(false);
            spawners[i].Enqueue(item);
        }

        // 스포너에서는 레이어 충돌: 미사일, 아이템 간에 무시
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("GameItem"), LayerMask.NameToLayer("Missile"), true);

    }

    // Update is called once per frame
    void Update()
    {

    }

#endregion

    #region Public Methods

    // item Type에 따라, Spawner에서 확인해보고, 없으면 생성 아니면 반환
    public override GameObject RentSpawner(ItemType type)
    {
        GameObject item = null;

        int spawnerIndex = (int)type;

        if (spawners[spawnerIndex].Count == 0)
        {
            item = Instantiate(ItemPrefabs[spawnerIndex]);

            item.name = ItemPrefabs[spawnerIndex].name + itemNamingNumber[spawnerIndex];
            itemNamingNumber[spawnerIndex]++;
            item.SetActive(false);

            return item;
        }

        item = spawners[spawnerIndex].Dequeue();

        item.transform.parent = null;
        item.SetActive(false);

        return item;
    }

    // 다시 풀에 되돌려주기
    public override void ReturnSpawner(ItemType type, GameObject item)
    {
        item.SetActive(false);

        Item itemcomponent = item.GetComponent<Item>();
        itemcomponent.ResetItemAlpha();


        // 아이템 스폰 체크 리스트 갱신
        if(itemcomponent != null)
        {
            int spawnidx = itemcomponent.SpawnTile;

            if (spawnidx > -1)
                itemSpawnTies[spawnidx] = false;
        }

        if(activeItems.Contains(item))
            activeItems.Remove(item);

        int spawnerIndex = (int)type;
        item.transform.parent = this.transform;
        spawners[spawnerIndex].Enqueue(item);

    }

    // 플랫폼 등록 함수
    public void SetPlatform(Platform platform)
    {
        gamePlatform = platform;
    }

    /// <summary>
    /// 확률계산 정상동작 확인용 100번 테스트 레이어
    /// </summary>
    public void TestPercent()
    {
        for(int i = 0; i < 100; ++i)
        {
            int randomPercent = (int)(Random.value * 100);
            int type = CalculatePercent(randomPercent);

            Debug.Log($"{i}'s time Percent is {randomPercent}, type is {type}");
        }
    }

    /// <summary>
    /// 부활 아이템을 먹고 확률을 재분배하기 위한 함수
    /// </summary>
    /// <remarks>
    /// Control, Slide, Lock 아이템에게 각 1%씩 분배 전달
    /// Revive 확률 제거
    /// </remarks>
    public void TakeRevive()
    {
        if (activeRevive == true)
            return;

        activeRevive = true;

        Item itemScript = null;

        for(int i = (int)ItemType.Control; i <= (int)ItemType.Lock; ++i)
        {
            itemScript = ItemPrefabs[i].GetComponent<Item>();

            itemScript.Percent += 1;
        }

        itemScript = ItemPrefabs[(int)ItemType.Revive].GetComponent<Item>();
        itemScript.Percent = 0;

        CalculatePercentBoundary();
    }

    public override void OnPlayerDeath()
    {
        base.OnPlayerDeath();
    }

    #endregion

    #region Private/Protected Methods

    protected override void StartProtocol()
    {

    }

    protected override void EndProtocol()
    {
        gamePlatform = null;

        while (activeItems.Count > 0)
        {
            GameObject item = activeItems.First.Value;
            activeItems.RemoveFirst();
            Destroy(item);
        }

        spawnRevive = false;
    }

    /// <summary>
    /// 바이너리 데이터로 직렬화된 아이템 데이터 동기화, 직접 프리팹과 연동
    /// </summary>
    /// <remarks>
    /// 프리팹의 이름을 통해 연결 시도
    /// </remarks>
    private void SyncItemData()
    {
       Dictionary<string, ItemData> itemDatas = GameData.Instance.ItemDatas();

        int curpercent = 0;

       for(int i = 0; i < ItemPrefabs.Count; ++i)
       {
            string itemName = ItemPrefabs[i].name;

            ItemData curItemData = itemDatas[itemName];

            Item curItem = ItemPrefabs[i].GetComponent<Item>();

            string dataPercent = curItemData.Percent;
            string dataValue = curItemData.Value;
            string dataTime = curItemData.Time;

           if(int.TryParse(dataPercent, out int percentint))
           {
                curItem.Percent = percentint;
                curpercent += percentint;
           }

            Debug.Log($"cur percent is {curpercent}");

           if(float.TryParse(dataValue, out float valuefloat))
           {
               curItem.SetValue(valuefloat);
           }

           if(float.TryParse(dataTime, out float timefloat))
           {
               curItem.SetTime(timefloat);
           }

            ItemPercent[i] = curItem.Percent;
       }

        CalculatePercentBoundary();
    }

    /// <summary>
    /// 랜덤값에 따른 확률을 계산하는 함수
    /// </summary>
    /// <param name="value">랜덤 생성된 값</param>
    /// <returns>확률 계산으로 선택된 아이템 index</returns>
    private int CalculatePercent(int value)
    {
        int itemIndex = 0;

        int percentStart = 0;

        for(int i = 0; i < ItemPrefabs.Count; ++i)
        {
            // ItemPercent[i]는 이미 누적 경계값
            int percentEnd = ItemPercent[i];

            if (percentStart <= value && value < percentEnd)
            {
                itemIndex = i;
                break;
            }
            else
            {
                percentStart = percentEnd;
            }
        }

        return itemIndex;
    }

    /// <summary>
    /// 아이템 확률 경계 계산
    /// </summary>
    private void CalculatePercentBoundary()
    {
        int percentStart = 0;

        for (int i = 0; i < ItemPrefabs.Count; ++i)
        {
            Item currentItem = ItemPrefabs[i].GetComponent<Item>();

            int percentEnd = percentStart + currentItem.Percent;

            ItemPercent[i] = percentEnd;

            // 확률이 0 ~ 100까지이기 때문에, 사용하지 않는 아이템은 101로 만듦
            if (currentItem.Percent == 0)
                ItemPercent[i] = 101;

            percentStart = percentEnd;

            Debug.Log($"{i}'s time Percent is {ItemPercent[i]}");

        }
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// 아이템 스폰 루프
    /// </summary>
    IEnumerator ItemSpawnLoop()
    {
        while (true)
        {
            int itemSpawnCount = Random.Range(1, maxItemSpawn);

            for(int i = 0; i < itemSpawnCount; ++i)
            {
                int randomrange = Random.Range(0, 99);

                bool researchFail = false;

                // 3번 재시도, 찾아도 없으면 다음 스폰으로 넘어가기
                if (itemSpawnTies[randomrange] == true)
                {
                    int count = 3;

                    while (itemSpawnTies[randomrange] == true && count > 0)
                    {
                        randomrange = Random.Range(0, 99);
                        --count;

                        if(count == 0)
                            researchFail = true;
                    }
                }

                if (researchFail)
                    continue;

                // 중복 체크
                itemSpawnTies[randomrange] = true;

                int itemPercent = (int)(Random.value * 100);
                int itemType = CalculatePercent(itemPercent);

                // 스폰 위치 계산
                Vector3 tilePos = gamePlatform.GetTile(randomrange).transform.position;
                tilePos.y += 2f;
                tilePos.x -= GlobalData.Instance.TileXScale / 2f;
                tilePos.z += GlobalData.Instance.TileZScale / 2f;

                GameObject item = RentSpawner((ItemType)itemType);

                item.transform.position = tilePos;

                item.GetComponent<Item>().SpawnTile = randomrange;

                activeItems.AddLast(item);

                item.SetActive(true);

            }

            yield return new WaitForSeconds(10f);
        }
    }

    #endregion

}
