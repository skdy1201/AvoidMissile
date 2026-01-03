using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 아이템 데이터 동기화 구조체
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

    [SerializeField] private List<int> itemNamingNumber = Enumerable.Repeat(0, System.Enum.GetValues(typeof(ItemType)).Length).ToList();

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
    /// 사망 이벤트 등록 및 아이템 풀 초기화
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        Player.OnPlayerDead.AddListener(OnPlayerDeath);

        SyncItemData();

        // 아이템 프리팹들을 하나씩 생성해 스포너에 집어넣기
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

        // 에디터에서의 레이어 설정이 이상해, 코드로 직접 설정
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("GameItem"), LayerMask.NameToLayer("Missile"), true);

    }

    // Update is called once per frame
    void Update()
    {

    }

#endregion

    #region Public Methods

    // item Type에 따라, Spawner를 확인해보고, 있으면 반환 없으면 생성
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

    // 다시 큐에 되돌려놓기
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
    /// 확률대로 나오는지 아이템 100번 시뮬레이션
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

        ReCalculateCumulativePercent();
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

    /// <summary>
    /// 바이너리 데이터로 동기화 해둔 아이템 설정 값을, 실제 프리팹과 연결
    /// </summary>
    /// <remarks>
    /// 아이템의 이름을 통해 값을 연결
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
                curItem.CumulativePercent = (percentint + curpercent);
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
       }
    }

    private int CalculatePercent(int value)
    {
        int itemIndex = 0;

        int percentStart = 0;
        int percentEnd = 0;

        for(int i = 0; i < ItemPrefabs.Count; ++i)
        {
            Item currentItem = ItemPrefabs[i].GetComponent<Item>();

            if(currentItem == null)
            {
                Debug.LogError("Not Item in ItemPrefabs");
            }

            percentEnd = currentItem.CumulativePercent;

            if (percentStart < value && value <= percentEnd)
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
    /// 확률 재계산 함수
    /// </summary>
    /// <remarks>
    /// 부활 아이템을 한 번 먹으면 확률을 0으로 만들어야 한다.
    /// 이후 랜덤 선택시 범위에 들어가지 않도록 누적확률을 조정
    /// </remarks>
    private void ReCalculateCumulativePercent()
    {
        // 누적 확률
        int percentStart = 0;

        for (int i = 0; i < ItemPrefabs.Count; ++i)
        {
            Item currentItem = ItemPrefabs[i].GetComponent<Item>();

            currentItem.CumulativePercent = percentStart + currentItem.Percent;

            // 스폰확률이 0이면, Random.Value에서 나오지 않도록 누적확률 조정(0 ~ 100)
            if (currentItem.Percent == 0)
                currentItem.CumulativePercent = 101;
            else
                percentStart = currentItem.CumulativePercent;

            Debug.Log($"item cumulativePercent is {currentItem.CumulativePercent}");

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

                // 3번 다시 찾는데 중복이라면 그냥 넘어가기
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

                // 타일 체크
                itemSpawnTies[randomrange] = true;

                int itemPercent = (int)Random.value * 100;
                int itemType = CalculatePercent(itemPercent);
                itemType = (int)ItemType.Revive;

                // 스폰 위치 재조정
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
