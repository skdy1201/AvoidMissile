using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ?꾩씠???곗씠??吏곷젹??援ъ“泥?
/// </summary>
public struct ItemData
{
    public string Name;
    public string Percent;
    public string Value;
    public string Time;
}

/// <summary>
/// ?꾩씠??????닿굅??
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
/// ?꾩씠???앹꽦 留ㅻ땲?
/// </summary>
/// <remarks>
/// ?꾩씠?쒕뱾???앹꽦 諛?愿由?諛?諛섑솚???대떦
/// </remarks>
public class ItemSpawner : Spawner<ItemType>
{
    #region Serialized Fields

    // ?꾩씠?쒕뱾???꾨━?뱀쑝濡?愿由?
    [SerializeField] List<GameObject> ItemPrefabs;

    // ??쇰뱾??醫뚰몴媛믪쓣 ??ν븯怨??ъ슜?섍린 ?꾪븳 ?뚮옯??
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

    // 遺???꾩씠?쒖쓣 癒뱀뿀?붿? 泥댄겕
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
    /// ?щ쭩 ?대깽???깅줉 諛??ㅻ툕?앺듃 ? 珥덇린??
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        Player.OnPlayerDead.AddListener(OnPlayerDeath);

        SyncItemData();

        // ?꾩씠???ㅽ룷?덈뱾???섎굹???ㅻ툕?앺듃 ?ㅽ룷?덉뿉 ?ｌ뼱?먭린
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

        // ?ㅽ룷?덉뿉?쒕뒗 ?덉씠??異⑸룎??誘몄궗?? ?꾩씠??媛꾩뿉 臾댁떆
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("GameItem"), LayerMask.NameToLayer("Missile"), true);

    }

    // Update is called once per frame
    void Update()
    {

    }

#endregion

    #region Public Methods

    // item Type???곕씪, Spawner?먯꽌 ?뺤씤?대낫怨? ?놁쑝硫??앹꽦 ?꾨땲硫?諛섑솚
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

    // ?ㅼ떆 ?먯뿉 ?섎룎?ㅼ＜湲?
    public override void ReturnSpawner(ItemType type, GameObject item)
    {
        item.SetActive(false);

        Item itemcomponent = item.GetComponent<Item>();
        itemcomponent.ResetItemAlpha();


        // ?꾩씠???ㅽ룿 泥댄겕 由ъ뒪??媛깆떊
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

    // ?뚮옯???깅줉 ?⑥닔
    public void SetPlatform(Platform platform)
    {
        gamePlatform = platform;
    }

    /// <summary>
    /// ?뺣쪧怨꾩궛 ?뺤긽?묐룞 ?뺤씤??100踰??쒕??덉씠??
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
    /// 遺???꾩씠?쒖쓣 癒밴퀬 ?뺣쪧???щ텇諛??섍린 ?꾪븳 ?⑥닔
    /// </summary>
    /// <remarks>
    /// Control, Slide, Lock ?꾩씠?쒖뿉寃?媛?1%??遺꾨같 ??
    /// Revive ?뺣쪧 ?쒓굅
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
    /// 諛붿씠?덈━ ?곗씠?곕줈 吏곷젹???대룄 ?곗씠???좎? ?덈맖, 吏곸젒 ?꾨━?밴낵 ?곕룞
    /// </summary>
    /// <remarks>
    /// ?꾨━?뱀쓽 ?대쫫???듯빐 ?곌껐 ?쒕룄
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
    /// ?쒕뜡媛믪뿉 ?곕Ⅸ ?뺣쪧??怨꾩궛?섎뒗 ?⑥닔
    /// </summary>
    /// <param name="value">?쒕뜡 ?앹꽦??媛?/param>
    /// <returns>?뺣쪧 怨꾩궛?쇰줈 ?좏깮???꾩씠??index</returns>
    private int CalculatePercent(int value)
    {
        int itemIndex = 0;

        int percentStart = 0;

        for(int i = 0; i < ItemPrefabs.Count; ++i)
        {
            // ItemPercent[i]???대? ?꾩쟻 寃쎄퀎媛?
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
    /// ?꾩씠???뺣쪧 怨꾩궛
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    private void CalculatePercentBoundary()
    {
        int percentStart = 0;

        for (int i = 0; i < ItemPrefabs.Count; ++i)
        {
            Item currentItem = ItemPrefabs[i].GetComponent<Item>();
            
            int percentEnd = percentStart + currentItem.Percent;

            ItemPercent[i] = percentEnd;

            // ?뺣쪧??0 ~ 100源뚯??닿린 ?뚮Ц?? ?ъ슜?섏? ?딅뒗 ?꾩씠?쒖? 101濡?留뚮벉
            if (currentItem.Percent == 0)
                ItemPercent[i] = 101;

            percentStart = percentEnd;

            Debug.Log($"{i}'s time Percent is {ItemPercent[i]}");

        }
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// ?꾩씠???ㅽ룿 猷⑦봽
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

                // 3占쏙옙 占쌕쏙옙 찾占승듸옙 占쌩븝옙占싱띰옙占?占쌓놂옙 占싼어가占쏙옙
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

                // ???泥댄겕
                itemSpawnTies[randomrange] = true;

                int itemPercent = (int)(Random.value * 100);
                int itemType = CalculatePercent(itemPercent);

                // ?ㅽ룿 ?꾩튂 怨꾩궛??
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
