using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// 미사일 타입 열거형
/// </summary>
public enum MissileType
{
    Falling,
    Hover
}

/// <summary>
/// X축 미사일 정보 구조체
/// </summary>
public struct XMissileInfo
{
    public int MissileHp;
    public float MissileMoveTime;
    public float MissileMoveSpeed;
    public float MissileRotateTime;
    public float MissileRotateSpeed;
}

/*
7. 🔧 실질적 개선 제안 (우선순위)
높음:

X축 미사일 스폰 로직의 엣지 케이스 처리
주석으로 설계 의도 명시 (왜 X/Y 다르게 처리하는지)

중간:

긴 메서드 분리 (SpawnMissile을 작은 단위로)
설정값들을 ScriptableObject로 완전 외부화

낮음:

매직넘버를 상수로 (선택사항)
이벤트 시스템 도입 (확장 시 고려)
*/

/// <summary>
/// 미사일 생성 및 관리 시스템
/// </summary>
///<remarks>
/// 미사일 생성 루프를 관리,
/// 미사일 오브젝트 풀을 설정,
/// 미사일 시스템에서 레벨 변경을 준비
/// X축은 현재 Spawner까지 사용할 필요가 없어 그냥 생성,제거
///</remarks>
public class MissileSpawner : Spawner<MissileType>
{
    #region Serialize Fields

    [Header("Reference")]
    [SerializeField] public GameObject yAxisMissilePrefab;
    [SerializeField] public GameObject xAxisMissilePrefab;
    [SerializeField] public Platform gamePlatform;

    [Header("Instantiate Setting")]
    [SerializeField] YAxisSetting yMissileSetting;
    [SerializeField] XAxisSetting xMissileSetting;
    [SerializeField] XMissileInfo xAxisMissileInfo;

    [SerializeField] FallingMissileSetting fallingMissileData;

    [Header("SpawnPoint")]
    [FormerlySerializedAs("xAsixmissileSpawnPoints")]
    [SerializeField] private List<GameObject> xAxismissileSpawnPoints = new List<GameObject>();

    [Header("Cur Spawn State")]
    [SerializeField] public int limitMissileCount;
    [SerializeField] public int minMissileCount;
    [SerializeField] public int limitMinMissileCount;
    [SerializeField] public int curMaxMissileCount;
    [SerializeField] public float maxMissileTimer;
    [SerializeField] public float missileCycle;
    [SerializeField] public float minFallSpeed;
    [SerializeField] public float maxFallSpeed;
    [SerializeField] private bool coroutineActive = false;

    [Header("Cur Queue Info")]
    [SerializeField] private int yAxisQueueSize;

    #endregion

    #region Private/Protected Fields

    /// <summary>
    /// 현재 활성화된 Y축 미사일
    /// </summary>
    private LinkedList<GameObject> currentYAxisMissiles = new LinkedList<GameObject>();

    /// <summary>
    /// 현재 활성화된 X축 미사일
    /// </summary>
    private LinkedList<GameObject> currnetXAxisMissiles = new LinkedList<GameObject>();

    /// <summary>
    /// 미사일 이름 지정을 위한 번호
    /// </summary>
    protected int missileNumber = 0;

    // 타일 하나의 크기
    private float tileX;
    private float tileZ;

    /// <summary>
    /// 스포너에 반환 대기중인 미사일들
    /// </summary>
    private List<GameObject> returnMissiles = new List<GameObject>();

    #endregion

    #region Properties

    /// <summary>
    /// 미사일 스포너 싱글톤 인스턴스
    /// </summary>
    public new static MissileSpawner Instance
    {
        get { return Singleton<MissileSpawner>.Instance; }
    }

    public FallingMissileSetting FallingData
    {
        set { fallingMissileData = value; }
    }

    /// <summary>
    /// 현재 최대 미사일 생성 개수
    /// </summary>
    public int CurMaxMissileCount
    {
        get => curMaxMissileCount;
        set => curMaxMissileCount = value + 1;
    }

    /// <summary>
    /// 최소 미사일 생성 개수
    /// </summary>
    public int MinMissileCount
    {
        get => minMissileCount;
        set => minMissileCount = value;
    }

    /// <summary>
    /// 최소 미사일 개수 제한
    /// </summary>
    public int LimitMinMissileCount
    {
        get => limitMinMissileCount;
        set => limitMinMissileCount = value;
    }

    /// <summary>
    /// 미사일 생성 주기
    /// </summary>
    public float MissileCycle
    {
        get => missileCycle;
        set => missileCycle = value;
    }

    /// <summary>
    /// 미사일 최소 낙하 속도 (units/second)
    /// </summary>
    public float MinFallSpeed
    {
        get => minFallSpeed;
        set => minFallSpeed = value;
    }

    /// <summary>
    /// 미사일 최대 낙하 속도 (units/second)
    /// </summary>
    public float MaxFallSpeed
    {
        get => maxFallSpeed;
        set => maxFallSpeed = value;
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 초기 설정 및 이벤트 등록
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        yMissileSetting = ScriptableObject.CreateInstance<YAxisSetting>();

        Player.OnPlayerDead.AddListener(OnPlayerDeath);

    }

    /// <summary>
    /// 반환 대기 중인 미사일 정리
    /// </summary>
    void Update()
    {
        yAxisQueueSize = spawners[(int)MissileType.Falling].Count;

        // 미사일 리스트가 25개 이상이면, 일괄 반환
        if (returnMissiles.Count >= 25)
        {
            for (int i = 0; i < returnMissiles.Count; i++)
            {
                if (returnMissiles[i] == null)
                {
                    Debug.LogWarning("during return missile is null");
                }

                ReturnSpawner(MissileType.Falling, returnMissiles[i]);
            }

            returnMissiles.Clear();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 미사일을 대여한다. 비어있다면 새로 생성한다.
    /// </summary>
    /// <param name="type"> 미사일 타입(열거형) </param>
    /// <returns> 미사일 오브젝트 </returns>
    public override GameObject RentSpawner(MissileType type)
    {
        int typeidx = (int)type;

        // 스포너가 비었다면, 생성
        if (spawners[typeidx].Count <= 0)
        {
            if (yAxisMissilePrefab == null)
            {
                Debug.LogWarning("[RentSpawner] yAxisMissilePrefab is NOT assigned!");
                return null;
            }

            GameObject obj = Instantiate(yAxisMissilePrefab);

            if (obj == null)
            {
                Debug.LogWarning("[RentSpawner] Instantiate failed!");
                return null;
            }

            obj.name = $"{obj.name}{missileNumber}";


            Missile missileComponent = obj.GetComponent<Missile>();
            if (missileComponent == null)
            {
                Debug.LogWarning("[RentSpawner] Missile component not found!");
                Destroy(obj);
                return null;
            }

            obj.GetComponent<Missile>().Number = missileNumber;
            missileNumber++;

            if (obj.GetComponent<FallingMissile>() != null)
                obj.GetComponent<FallingMissile>().Returned = false;

            return obj;
        }

        GameObject cur = spawners[typeidx].Dequeue();

        if (cur == null)
        {
            Debug.LogWarning("[RentSpawner] Pool Rent error!");
            return null;
        }

        cur.transform.SetParent(null);

        return cur;
    }

    /// <summary>
    /// 미사일을 스포너에 반환
    /// </summary>
    /// <param name="type"> 미사일 타입(열거형) </param>
    /// <param name="missile"> 미사일 오브젝트 </param>
    public override void ReturnSpawner(MissileType type, GameObject missile)
    {
        int typeidx = (int)type;

        if (missile == null)
        {
            Debug.LogWarning("Before ReturnSpawner problem");
            return;
        }

        missile.SetActive(false);

        spawners[typeidx].Enqueue(missile);

        if (currentYAxisMissiles.Contains(missile))
            currentYAxisMissiles.Remove(missile);

        missile.transform.SetParent(this.gameObject.transform);
    }

    /// <summary>
    /// 미사일을 관리하는 컨테이너를 변경
    /// </summary>
    /// <param name="obj"> 미사일 오브젝트 </param>
    public void ReserveReturn(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("ReserveRetrun problem");
        }

        if (obj.GetComponent<Missile>().Returned == false)
        {
            obj.GetComponent<Missile>().Returned = true;
            currentYAxisMissiles.Remove(obj);
            returnMissiles.Add(obj);
        }
    }

    /// <summary>
    /// Spawn Point for XAxisMissile
    /// </summary>
    /// <param name="gameObject"> 4 point </param>
    public void AddXSpawnPoint(GameObject gameObject) => xAxismissileSpawnPoints.Add(gameObject);

    /// <summary>
    /// Xaxis Missile not use Pool Yet
    /// </summary>
    /// <param name="gameObject"> XAxis Missile </param>
    public void SubSpawn(GameObject gameObject)
    {
        if (currnetXAxisMissiles.Find(gameObject) != null)
        {
            currnetXAxisMissiles.Remove(gameObject);
        }
    }

    /// <summary>
    /// 설정을 업데이트
    /// </summary>
    public void UpdateSetting()
    {
        // HP 업데이트
        if (xMissileSetting.MaxLimitHP > xAxisMissileInfo.MissileHp)
            xAxisMissileInfo.MissileHp += 1;
        else
            xAxisMissileInfo.MissileHp = xMissileSetting.MaxLimitHP;

        // 이동 속도 업데이트
        if (xMissileSetting.MaxLimitMoveSpeed > xAxisMissileInfo.MissileMoveSpeed)
            xAxisMissileInfo.MissileMoveSpeed += 0.25f;
        else
            xAxisMissileInfo.MissileMoveSpeed = xMissileSetting.MaxLimitMoveSpeed;

        // 회전 속도 업데이트
        if (xMissileSetting.MaxLimitRotateSpeed > xAxisMissileInfo.MissileRotateSpeed)
            xAxisMissileInfo.MissileRotateSpeed += 15f;
        else
            xAxisMissileInfo.MissileRotateSpeed = xMissileSetting.MaxLimitRotateSpeed;

        // 회전 시간 업데이트
        if (xMissileSetting.MinRotateTime < xAxisMissileInfo.MissileRotateTime)
            xAxisMissileInfo.MissileRotateSpeed -= 0.25f;
        else
            xAxisMissileInfo.MissileRotateSpeed = xMissileSetting.MinRotateTime;
    }

    /// <summary>
    /// 추적 미사일 무작위 설정을 적용
    /// </summary>
    /// <param name="missile"> 미사일 오브젝트 </param>
    public void GetRandomSettingHoming(HomingMissile missile)
    {
        int healthPoint = Random.Range(1, xAxisMissileInfo.MissileHp);
        float moveTime = Random.Range(xAxisMissileInfo.MissileMoveTime / 2f, xAxisMissileInfo.MissileMoveTime);
        float moveSpeed = Random.Range(xAxisMissileInfo.MissileMoveSpeed / 2f, xAxisMissileInfo.MissileMoveSpeed);
        float rotateTime = Random.Range(xAxisMissileInfo.MissileRotateTime, xAxisMissileInfo.MissileRotateTime * 2);
        float rotateSpeed = Random.Range(180f, xAxisMissileInfo.MissileRotateSpeed);

        missile.SetStat(healthPoint, moveTime, moveSpeed, rotateTime, rotateSpeed);
        missile.Initialize(moveSpeed);
    }

    /// <summary>
    /// 플레이어 사망 시 미사일 생성 중지
    /// </summary>
    public override void OnPlayerDeath()
    {
        base.OnPlayerDeath();
    }

    /// <summary>
    /// 미사일 생성 개수 제한 변경
    /// </summary>
    /// <param name="count"> 개수 </param>
    public void SetMissileSpawnCount(int count) => limitMissileCount = count + 1;

    /// <summary>
    /// 미사일 생성 타이머 설정
    /// </summary>
    /// <param name="timer"> 타이머 값 </param>
    public void SetMissileSpawnTimer(float timer) => maxMissileTimer = timer;

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 미사일 스포너 설정 및 플레이 씬에서 루프 시작
    /// </summary>
    protected override void StartProtocol()
    {
        if (SceneManager.GetActiveScene().name == GlobalData.Instance.PlayScene)
        {
            InitializeSettings();

            // 미사일 생성 코루틴 시작
            if (coroutineActive == false)
            {
                StartCoroutine(MissileSpawnLoop());
                coroutineActive = true;
            }

            // Y축 미사일 풀 초기화
            if (spawners[(int)MissileType.Falling].Count <= 0)
            {
                for (int i = 0; i < 100; ++i)
                {
                    GameObject missileObject = Instantiate(yAxisMissilePrefab);
                    spawners[(int)MissileType.Falling].Enqueue(missileObject);
                    missileObject.transform.parent = this.transform;
                    missileObject.SetActive(false);

                    missileObject.name = $"{missileObject.name}{missileNumber}";

                    missileNumber++;
                }
            }

            // 타일 크기 가져오기
            Vector3 worldSize = Vector3.Scale(GlobalData.Instance.TilePrefab.GetComponent<MeshFilter>().sharedMesh.bounds.size, transform.lossyScale);
            tileX = worldSize.x;
            tileZ = worldSize.z;
        }
    }

    /// <summary>
    /// Y축 미사일 반환 및 X축 미사일 제거
    /// </summary>
    protected override void EndProtocol()
    {
        gamePlatform = null;
        xAxismissileSpawnPoints.Clear();
        coroutineActive = false;

        // Y축 미사일 반환
        var currentNode = currentYAxisMissiles.First;

        while (currentNode != null)
        {
            var currentYMissile = currentNode.Value;
            var nextNode = currentNode.Next;
            currentYAxisMissiles.Remove(currentNode);

            Destroy(currentYMissile);

            currentNode = nextNode;
        }

        // X축 미사일 제거
        currentNode = currnetXAxisMissiles.First;

        while (currentNode != null)
        {
            var cur = currentNode;
            currentNode = currentNode.Next;
            Destroy(cur.Value);
        }

        // 반환 대기중인 미사일 처리
        if (returnMissiles.Count > 0)
        {
            for (int i = 0; i < returnMissiles.Count; i++)
            {
               Destroy(returnMissiles[i]);
            }

            returnMissiles.Clear();
        }

        // 큐도 정리
        while (spawners[(int)MissileType.Falling].Count > 0)
        {
            GameObject missile = spawners[(int)MissileType.Falling].Dequeue();
            if (missile != null)
                Destroy(missile);
        }

        missileNumber = 0;
    }

    /// <summary>
    /// 기본 미사일 설정값 초기화
    /// </summary>
    private void InitializeSettings()
    {
        limitMissileCount = yMissileSetting.LimitMissileCount;
        minMissileCount = yMissileSetting.MinMissileCount;
        limitMinMissileCount = yMissileSetting.LimitMinMissileCount;
        curMaxMissileCount = yMissileSetting.CurMaxMissileCount;
        maxMissileTimer = yMissileSetting.MaxMissileTimer;
        missileCycle = yMissileSetting.MissileCycle;
        minFallSpeed = yMissileSetting.MinFallSpeed;
        maxFallSpeed = yMissileSetting.MaxFallSpeed;

        xAxisMissileInfo = new XMissileInfo
        {
            MissileHp = 1,
            MissileMoveTime = 5f,
            MissileMoveSpeed = 5f,
            MissileRotateTime = 3.5f,
            MissileRotateSpeed = 180f,
        };
    }

    /// <summary>
    /// 미사일 한 주기
    /// </summary>
    private void SpawnMissile()
    {
        // 사전 체크
        if (gamePlatform == null)
        {
            Debug.LogError("[SpawnMissile] gamePlatform is null!");
            return;
        }

        if (GlobalData.Instance == null)
        {
            Debug.LogError("[SpawnMissile] GlobalData.Instance is null!");
            return;
        }

        if (GameData.Instance == null)
        {
            Debug.LogError("[SpawnMissile] GameData.Instance is null!");
            return;
        }


        int missileCount = Random.Range(minMissileCount, curMaxMissileCount);

        // 동일 타일 생성 방지를 위한 체크
        HashSet<int> tileIndexes = new HashSet<int>();

        for (int i = 0; i < missileCount; ++i)
        {
            int spawnTileid = Random.Range(0, gamePlatform.TileSize + 1);

            // 이미 사용된 타일이면 건너뛰기
            if (tileIndexes.Contains(spawnTileid))
            {
                continue;
            }

            tileIndexes.Add(spawnTileid);

            GameObject spawnTile = gamePlatform.GetTile(spawnTileid);

            if(spawnTile == null)
            {
                Debug.LogError("spawnTile is null");
                continue;
            }

            Vector3 tileTransform = spawnTile.transform.position;

            // 생성 지점 조정
            tileTransform.y += GlobalData.Instance.MissileDropPoint;
            tileTransform.x -= tileX / 2;
            tileTransform.z += tileZ / 2;

            // 미사일을 풀에서 꺼내기
            GameObject missileObject = RentSpawner(MissileType.Falling);

            if (missileObject == null)
            {
                Debug.LogError("[SpawnMissile] RentSpawner returned null!");
                continue;
            }

            if (missileObject.GetComponent<FallingMissile>() != null)
            {
                missileObject.GetComponent<FallingMissile>().SpawnTime = missileObject.GetComponent<FallingMissile>().SpawnTime + 1;
            }

            missileObject.transform.position = tileTransform;

            float alpha = GameData.Instance.GetSettingValue(OptionType.Alpha);

            if (alpha != missileObject.GetComponent<FallingMissile>().GetAlpha())
            {
                missileObject.GetComponent<FallingMissile>().ChangeAlpha(alpha);
            }

            Vector2 xzCoordinate = new Vector2(tileTransform.x, tileTransform.z);
            FallingMissile yAxisMissile = missileObject.GetComponent<FallingMissile>();
            yAxisMissile.XZCoord = xzCoordinate;

            // 낙하 속도 설정 (units/second)
            float randomSpeed = Random.Range(minFallSpeed, maxFallSpeed);
            yAxisMissile.Initialize(randomSpeed);

            missileObject.SetActive(true);

            currentYAxisMissiles.AddLast(missileObject);
        }
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// Y축 미사일 생성 루프
    /// </summary>
    /// <returns> 코루틴 </returns>
    IEnumerator MissileSpawnLoop()
    {
        // 플레이 씬 동안 계속 진행
        while (true)
        {
            // 미사일 생성 타이머 설정
            float missileTimer = Random.Range(2f, missileCycle);
            missileTimer = Mathf.Floor(missileTimer * 100) / 100f;

            yield return new WaitForSeconds(missileTimer);
            
            SpawnMissile();

            // 다음 주기 대기 시간
            float waitTimer = Random.Range(0f, 5f);
           
            yield return new WaitForSeconds(waitTimer);
        }
    }

    /// <summary>
    /// X축 미사일 생성 루프
    /// </summary>
    /// <returns> 코루틴 </returns>
    IEnumerator XAxisMissileSpawnLoop()
    {
        while (true)
        {
            int spawnNumber = Mathf.RoundToInt(Mathf.Clamp((Random.Range(1f, xAxismissileSpawnPoints.Count)), 1, xAxismissileSpawnPoints.Count - 1));

            // 동일 위치 확인
            HashSet<int> spawnPointNum = new HashSet<int>();

            for (int i = 0; i < spawnNumber; i++)
            {
                int spawnTileid = Random.Range(0, xAxismissileSpawnPoints.Count);

                // 중복 위치 회피
                if (spawnPointNum.Contains(spawnTileid))
                {
                    while (true)
                    {
                        spawnTileid = Random.Range(0, xAxismissileSpawnPoints.Count);

                        if (!spawnPointNum.Contains(spawnTileid))
                            break;

                    }
                }

                spawnPointNum.Add(spawnTileid);

                // Y좌표 조정
                Vector3 spawnPosition = xAxismissileSpawnPoints[spawnTileid].transform.position;
                spawnPosition.y += 2.5f;

                GameObject xAxisMissile = Instantiate(xAxisMissilePrefab);

                xAxisMissile.transform.position = spawnPosition;

                currnetXAxisMissiles.AddLast(xAxisMissile);
            }

            // 다음 사이클 고정 대기 시간
            yield return new WaitForSeconds(15f);

        }
    }

    #endregion

}
