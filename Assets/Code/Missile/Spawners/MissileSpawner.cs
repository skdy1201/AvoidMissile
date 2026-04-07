using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 미사일 타입 열거형
/// </summary>
public enum MissileType
{
    Falling,
    Hover,
    Grand
}

/// <summary>
/// 스폰 스케줄 — 시간과 미사일 타입을 묶어 큐에 삽입
/// </summary>
public struct SpawnSchedule
{
    public float Time;
    public MissileType Type;

    public SpawnSchedule(float time, MissileType type)
    {
        Time = time;
        Type = type;
    }
}

/// <summary>
/// 미사일 생성 및 관리 시스템
/// </summary>
///<remarks>
/// 미사일 생성 루프를 관리,
/// 미사일 오브젝트 풀을 설정,
/// 미사일 시스템에서 레벨 변경을 준비
/// 추적 미사일은 현재 Spawner까지 사용할 필요가 없어 그냥 생성,제거
///</remarks>
public partial class MissileSpawner : Spawner<MissileType>
{
    #region Serialize Fields

    [Header("Reference")]
    [SerializeField] public GameObject fallingMissilePrefab;
    [SerializeField] public GameObject hoverMissilePrefab;
    [SerializeField] public GameObject grandMissilePrefab;
    [SerializeField] public Platform gamePlatform;

    [Header("Instantiate Setting")]
    [SerializeField] FallingMissileSetting fallingMissileData;
    [SerializeField] HoverMissileSetting hoverMissileData;
    [SerializeField] GrandMissileSetting grandMissileData;

    [Header("SpawnPoint")]
    [SerializeField] private List<GameObject> hoverMissileSpawnPoints = new List<GameObject>();
    private SpawnPointGroup spawnPointGroup;


    [Header("Spawn State")]
    [SerializeField] private bool homingLoop = false;
    [SerializeField] private bool fallingLoop = false;
    [SerializeField] private bool grandLoop = false;

    [SerializeField] private int fallingQueueSize;

    #endregion

    #region Private/Protected Fields

    /// <summary>
    /// 낙하 미사일 기본값 (초기화용)
    /// </summary>
    private FallingMissileSetting defaultFallingData;

    /// <summary>
    /// 추적 미사일 기본값 (초기화용)
    /// </summary>
    private HoverMissileSetting defaultHoverData;

    /// <summary>
    /// 대형 미사일 기본값 (초기화용)
    /// </summary>
    private GrandMissileSetting defaultGrandData;

    /// <summary>
    /// 현재 활성화된 낙하 미사일
    /// </summary>
    private LinkedList<GameObject> currentFallingMissiles = new LinkedList<GameObject>();

    /// <summary>
    /// 현재 활성화된 추적 미사일
    /// </summary>
    private LinkedList<GameObject> currentHoverMissiles = new LinkedList<GameObject>();

    /// <summary>
    /// 현재 활성화된 대형 미사일
    /// </summary>
    private LinkedList<GameObject> currentGrnadMissiles = new LinkedList<GameObject>();

    /// <summary>
    /// 미사일 이름 지정을 위한 번호
    /// </summary>
    protected int missileNumber = 0;

    // 타일 하나의 크기
    private float tileX;
    private float tileZ;

    /// <summary>
    /// 스폰 스케줄 큐 — 시간순으로 (시간, 타입) 쌍을 관리
    /// </summary>
    private Queue<SpawnSchedule> spawnQueue = new Queue<SpawnSchedule>();

    private float spawnTimer;
    private bool spawning;
    private const int MaxQueueSize = 20;

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

    /// <summary>
    /// 낙하 미사일 초기 데이터 설정
    /// </summary>
    public FallingMissileSetting FallingData
    {
        set
        {
            fallingMissileData = value;
            defaultFallingData = value;
        }
    }

    /// <summary>
    /// 추적 미사일 초기 데이터 설정
    /// </summary>
    public HoverMissileSetting HoverData
    {
        set
        {
            hoverMissileData = value;
            defaultHoverData = value;
        }
    }

    /// <summary>
    /// 대형 미사일 초기 데이터 설정
    /// </summary>
    public GrandMissileSetting GrandData
    {
        set
        {
            grandMissileData = value;
            defaultGrandData = value;
        }
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 초기 설정 및 이벤트 등록
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        Player.OnPlayerDead.AddListener(OnPlayerDeath);

    }

    /// <summary>
    /// 큐 소비 + 패턴 틱 + 반환 대기 미사일 정리
    /// </summary>
    void Update()
    {
        fallingQueueSize = spawners[(int)MissileType.Falling].Count;

        if (inPattern) TickPattern();

        // 큐 기반 스폰 루프
        if (spawning)
        {
            if (spawnQueue.Count == 0)
                FillSpawnQueue();

            spawnTimer += Time.deltaTime;

            while (spawnQueue.Count > 0 && spawnQueue.Peek().Time <= spawnTimer)
            {
                SpawnSchedule schedule = spawnQueue.Dequeue();
                switch (schedule.Type)
                {
                    case MissileType.Falling: SpawnFallingMissiles(); break;
                    case MissileType.Hover:   SpawnHoverMissiles();   break;
                    case MissileType.Grand:   SpawnGrandMissiles();   break;
                }
            }

            // 큐 소진 시 랜덤 스폰 + 타이머 리셋 + 보충
            if (spawnQueue.Count == 0)
            {
                SpawnRandomOnQueueEmpty();
                spawnTimer = 0f;
                FillSpawnQueue();
            }
        }

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
            if (fallingMissilePrefab == null)
            {
                Debug.LogWarning("[RentSpawner] fallingMissilePrefab is NOT assigned!");
                return null;
            }

            GameObject obj = Instantiate(fallingMissilePrefab);

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

        if (currentFallingMissiles.Contains(missile))
            currentFallingMissiles.Remove(missile);

        missile.transform.SetParent(this.gameObject.transform);
    }

    /// <summary>
    /// 추적 미사일 스폰 포인트 추가
    /// </summary>
    /// <param name="gameObject"> 스폰 포인트 </param>
    public void AddHoverSpawnPoint(GameObject gameObject) => hoverMissileSpawnPoints.Add(gameObject);

    /// <summary>
    /// 추적 미사일을 활성 목록에서 제거
    /// </summary>
    /// <param name="gameObject"> 추적 미사일 </param>
    public void RemoveHoverMissile(GameObject gameObject)
    {
        if (currentHoverMissiles.Find(gameObject) != null)
        {
            currentHoverMissiles.Remove(gameObject);
        }
    }

    /// <summary>
    /// 레벨에 따라 미사일 설정을 업데이트
    /// </summary>
    public void UpdateSetting(int level)
    {
        UpdateFallingMissile();
        UpdateHoverMissile(level);
        UpdateGrandMissile(level);
    }

    /// <summary>
    /// 낙하 미사일 설정 업데이트
    /// </summary>
    private void UpdateFallingMissile()
    {
        fallingMissileData.missileCount = Mathf.Min(fallingMissileData.missileCount + fallingMissileData.missileIncrement, fallingMissileData.maxCount);
        fallingMissileData.fallSpeed = Mathf.Min(fallingMissileData.fallSpeed + fallingMissileData.fallIncrement, fallingMissileData.fallSpeedMax);
        fallingMissileData.waiting = Mathf.Max(fallingMissileData.waiting + fallingMissileData.waitIncrement, fallingMissileData.waitingMax);
    }

    /// <summary>
    /// 추적 미사일 설정 업데이트
    /// </summary>
    /// <remarks>
    /// 레벨 10 미만: 업데이트 안 함
    /// 레벨 10: 스폰 루프 시작
    /// 레벨 11 이상: 스탯 업데이트
    /// </remarks>
    private void UpdateHoverMissile(int level)
    {
        if (level < 10)
            return;

        // 레벨 10에서 루프 시작
        if (level == 10 && homingLoop == false)
        {
            StartCoroutine(HoverMissileSpawnLoop());
            homingLoop = true;
            return;
        }

        // 레벨 11 이상부터 스탯 업데이트
        // 증가 스탯: Min으로 최대값 제한
        hoverMissileData.hp = Mathf.Min(hoverMissileData.hp + hoverMissileData.hpIncrement, hoverMissileData.hpMax);
        hoverMissileData.flight = Mathf.Min(hoverMissileData.flight + hoverMissileData.flightIncrement, hoverMissileData.flightMax);
        hoverMissileData.flightSpeed = Mathf.Min(hoverMissileData.flightSpeed + hoverMissileData.flightSpeedIncrement, hoverMissileData.flightSpeedMax);
        hoverMissileData.turnRate = Mathf.Min(hoverMissileData.turnRate + hoverMissileData.turnRateIncrement, hoverMissileData.turnRateMax);

        // 감소 스탯: Max로 최소값 제한 (turnIncrement가 음수, turnMax가 실제 최소값)
        hoverMissileData.turn = Mathf.Max(hoverMissileData.turn + hoverMissileData.turnIncrement, hoverMissileData.turnMax);
    }

    private void UpdateGrandMissile(int level)
    {
        if (level < 25)
            return;

        // 레벨 25에서 루프 시작
        if (level == 25 && grandLoop == false)
        {
            StartCoroutine(GrandMissileSpawnLoop());
            grandLoop = true;
            return;
        }

        // 레벨 25 이상부터 5레벨 단위로 업데이트
        if (level % 5 != 0)
            return;
        grandMissileData.count = Mathf.Min(grandMissileData.count + grandMissileData.countIncrement, grandMissileData.maxCount);
        grandMissileData.speed = Mathf.Min(grandMissileData.speed + grandMissileData.speedIncrement, grandMissileData.speedMax);
        grandMissileData.diameter = Mathf.Min(grandMissileData.diameter + grandMissileData.diameterIncrement, grandMissileData.diameterMax);

    }

    /// <summary>
    /// 추적 미사일 무작위 설정을 적용
    /// </summary>
    /// <param name="missile"> 미사일 오브젝트 </param>
    public void GetRandomSettingHoming(HoverMissile missile)
    {
        int healthPoint = Random.Range(1, hoverMissileData.hp + 1);
        float moveTime = Mathf.Round(Random.Range(hoverMissileData.flight / 2f, hoverMissileData.flight) * 100f) / 100f;
        float moveSpeed = Mathf.Round(Random.Range(hoverMissileData.flightSpeed / 2f, hoverMissileData.flightSpeed) * 100f) / 100f;
        float rotateTime = Mathf.Round(Random.Range(hoverMissileData.turn / 2f, hoverMissileData.turn) * 100f) / 100f;
        float rotateSpeed = Mathf.Round(Random.Range(hoverMissileData.turnRate / 2f, hoverMissileData.turnRate) * 100f) / 100f;

        missile.SetStat(healthPoint, moveTime, moveSpeed, rotateTime, rotateSpeed);
        missile.Initialize();
    }

    /// <summary>
    /// 플레이어 사망 시 미사일 생성 중지
    /// </summary>
    public override void OnPlayerDeath()
    {
        base.OnPlayerDeath();
        fallingLoop = false;
        homingLoop = false;
        grandLoop = false;
    }

    /// <summary>
    /// 부활 시 미사일 루프 재시작
    /// </summary>
    /// <param name="level">현재 레벨</param>
    public void RestartMissileLoops(int level)
    {
        if (fallingLoop == false)
        {
            StartCoroutine(FallingMissileSpawnLoop());
            fallingLoop = true;
        }

        if (level >= 10 && homingLoop == false)
        {
            StartCoroutine(HoverMissileSpawnLoop());
            homingLoop = true;
        }

        if(level >= 25 && grandLoop == false)
        {
            StartCoroutine(GrandMissileSpawnLoop());
            grandLoop = true;
        }
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 미사일 스포너 설정 및 플레이 씬에서 루프 시작
    /// </summary>
    protected override void StartProtocol()
    {
        if (SceneManager.GetActiveScene().name == GlobalData.Instance.PlayScene)
        {

            // TODO: 테스트용 주석 처리 — 패턴 시스템 검증 후 복구
            // if (fallingLoop == false)
            // {
            //     StartCoroutine(FallingMissileSpawnLoop());
            //     fallingLoop = true;
            // }

            // 낙하 미사일 풀 초기화
            if (spawners[(int)MissileType.Falling].Count <= 0)
            {
                for (int i = 0; i < 100; ++i)
                {
                    GameObject missileObject = Instantiate(fallingMissilePrefab);
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

            CreateSpawnPoint();

            // 큐 기반 스폰 시작
            spawning = true;
            FillSpawnQueue();

            // TODO: 테스트용 — 패턴 시스템 검증 후 제거
            SpawnPattern();
        }
    }

    /// <summary>
    /// 낙하 미사일 반환 및 추적 미사일 제거
    /// </summary>
    protected override void EndProtocol()
    {
        gamePlatform = null;
        hoverMissileSpawnPoints.Clear();

        // 플래그 리셋 (OnPlayerDeath와 중복)
        // 사망 없이 게임 종료 시(일시정지에서 나가기 등) 대응
        fallingLoop = false;
        homingLoop = false;
        grandLoop = false;

        // 미사일 데이터 초기화
        fallingMissileData = defaultFallingData;
        hoverMissileData = defaultHoverData;
        grandMissileData = defaultGrandData;

        // 낙하 미사일 반환
        var currentNode = currentFallingMissiles.First;

        while (currentNode != null)
        {
            var currentMissile = currentNode.Value;
            var nextNode = currentNode.Next;
            currentFallingMissiles.Remove(currentNode);

            Destroy(currentMissile);

            currentNode = nextNode;
        }

        // 추적 미사일 제거
        currentNode = currentHoverMissiles.First;

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
    /// 8방향(직선 4 + 대각선 4)의 스폰포인트를 생성
    /// </summary>
    private void CreateSpawnPoint()
    {
        float tileXScale = GlobalData.Instance.TileXScale;
        float tileZScale = GlobalData.Instance.TileZScale;

        GameObject rootObject = new GameObject("SpawnPoints");
        spawnPointGroup = rootObject.AddComponent<SpawnPointGroup>();
        Transform root = rootObject.transform;

        //north
        Vector3 basePoint = gamePlatform.GetTile(0).transform.position;
        basePoint.z += 10f;
        basePoint.x -= tileXScale / 2f;

        for(int i = 0; i < 10; ++i)
        {
            GameObject gameObject = new GameObject("northPoint" + i);
            gameObject.transform.position = basePoint + new Vector3(tileXScale * i, 0f, 0f);
            gameObject.transform.SetParent(root);

            spawnPointGroup.NorthPoints.Add(gameObject);
        }

        //south
        basePoint = gamePlatform.GetTile(90).transform.position;
        basePoint.z -= 10f;
        basePoint.x -= tileXScale / 2f;

        for (int i = 0; i < 10; ++i)
        {
            GameObject gameObject = new GameObject("southPoint" + i);
            gameObject.transform.position = basePoint + new Vector3(tileXScale * i, 0f, 0f);
            gameObject.transform.SetParent(root);

            spawnPointGroup.SouthPoints.Add(gameObject);
        }

        //east
        basePoint = gamePlatform.GetTile(9).transform.position;
        basePoint.x += 10f;
        basePoint.z += tileZScale / 2f;

        for (int i = 0; i < 10; ++i)
        {
            GameObject gameObject = new GameObject("eastPoint" + i);
            gameObject.transform.position = basePoint + new Vector3(0f, 0f, -tileZScale * i);
            gameObject.transform.SetParent(root);

            spawnPointGroup.EastPoints.Add(gameObject);
        }

        //west
        basePoint = gamePlatform.GetTile(0).transform.position;
        basePoint.x -= 10f;
        basePoint.z += tileZScale / 2f;

        for (int i = 0; i < 10; ++i)
        {
            GameObject gameObject = new GameObject("westPoint" + i);
            gameObject.transform.position = basePoint + new Vector3(0f, 0f, -tileZScale * i);
            gameObject.transform.SetParent(root);

            spawnPointGroup.WestPoints.Add(gameObject);
        }

        //diagonal - 코너당 1개씩, 플랫폼 중앙 방향으로 이동
        GameObject diagonalRoot = new GameObject("DiagonalPoints");
        diagonalRoot.transform.SetParent(root);
        Transform diagRoot = diagonalRoot.transform;

        //ne: GetTile(9) 기준, center 보정 후 (+10, 0, +10)
        basePoint = gamePlatform.GetTile(9).transform.position;
        basePoint.x -= tileXScale / 2f;
        basePoint.z += tileZScale / 2f;
        basePoint.x += 10f;
        basePoint.z += 10f;

        {
            GameObject gameObject = new GameObject("nePoint0");
            gameObject.transform.position = basePoint;
            gameObject.transform.SetParent(diagRoot);
            spawnPointGroup.NEPoints.Add(gameObject);
        }

        //nw: GetTile(0) 기준, center 보정 후 (-10, 0, +10)
        basePoint = gamePlatform.GetTile(0).transform.position;
        basePoint.x -= tileXScale / 2f;
        basePoint.z += tileZScale / 2f;
        basePoint.x -= 10f;
        basePoint.z += 10f;

        {
            GameObject gameObject = new GameObject("nwPoint0");
            gameObject.transform.position = basePoint;
            gameObject.transform.SetParent(diagRoot);
            spawnPointGroup.NWPoints.Add(gameObject);
        }

        //se: GetTile(99) 기준, center 보정 후 (+10, 0, -10)
        basePoint = gamePlatform.GetTile(99).transform.position;
        basePoint.x -= tileXScale / 2f;
        basePoint.z += tileZScale / 2f;
        basePoint.x += 10f;
        basePoint.z -= 10f;

        {
            GameObject gameObject = new GameObject("sePoint0");
            gameObject.transform.position = basePoint;
            gameObject.transform.SetParent(diagRoot);
            spawnPointGroup.SEPoints.Add(gameObject);
        }

        //sw: GetTile(90) 기준, center 보정 후 (-10, 0, -10)
        basePoint = gamePlatform.GetTile(90).transform.position;
        basePoint.x -= tileXScale / 2f;
        basePoint.z += tileZScale / 2f;
        basePoint.x -= 10f;
        basePoint.z -= 10f;

        {
            GameObject gameObject = new GameObject("swPoint0");
            gameObject.transform.position = basePoint;
            gameObject.transform.SetParent(diagRoot);
            spawnPointGroup.SWPoints.Add(gameObject);
        }

    }

    /// <summary>
    /// 낙하 미사일 한 주기 스폰
    /// </summary>
    private void SpawnFallingMissiles()
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


        int missileCount = Random.Range(fallingMissileData.missileCount / 2, fallingMissileData.missileCount + 1);

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
            FallingMissile fallingMissile = missileObject.GetComponent<FallingMissile>();
            fallingMissile.XZCoord = xzCoordinate;

            // 낙하 속도 설정 (units/second)
            float randomSpeed = Mathf.Round(Random.Range(fallingMissileData.fallSpeed / 2f, fallingMissileData.fallSpeed) * 100f) / 100f;
            fallingMissile.SetStat(randomSpeed);
            fallingMissile.Initialize();

            missileObject.SetActive(true);

            currentFallingMissiles.AddLast(missileObject);
        }
    }

    /// <summary>
    /// 추적 미사일 한 주기 스폰
    /// </summary>
    private void SpawnHoverMissiles()
    {
        int spawnNumber = Random.Range(1, 5);

        // 동일 위치 확인
        HashSet<int> spawnPointNum = new HashSet<int>();
        int pointsPerDirection = spawnPointGroup.NorthPoints.Count;
        int totalPoints = pointsPerDirection * 4;

        for (int i = 0; i < spawnNumber; i++)
        {
            int spawnTileid = Random.Range(0, totalPoints);

            // 중복 위치 회피
            if (spawnPointNum.Contains(spawnTileid))
            {
                while (true)
                {
                    spawnTileid = Random.Range(0, totalPoints);

                    if (!spawnPointNum.Contains(spawnTileid))
                        break;
                }
            }

            spawnPointNum.Add(spawnTileid);

            // Y좌표 조정
            Vector3 spawnPosition = spawnPointGroup.GetPointPosition(spawnTileid / pointsPerDirection, spawnTileid % pointsPerDirection);
            spawnPosition.y += 2.5f;

            GameObject hoverMissile = Instantiate(hoverMissilePrefab);
            hoverMissile.transform.position = spawnPosition;

            HoverMissile hoverMissileComponent = hoverMissile.GetComponent<HoverMissile>();
            hoverMissileComponent.SetHoverType((HoverMissileType)Random.Range(
                (int)HoverMissileType.Custom,
                (int)HoverMissileType.HorizonLinear + 1));
            GetRandomSettingHoming(hoverMissileComponent);
            hoverMissileComponent.SetSpawnDirection(spawnTileid / pointsPerDirection);

            currentHoverMissiles.AddLast(hoverMissile);
        }
    }

    /// <summary>
    /// 대형 미사일 한 주기 스폰
    /// </summary>
    private void SpawnGrandMissiles()
    {
        int spawnCount = Random.Range(1, grandMissileData.count + 1);

        for (int i = 0; i < spawnCount; i++)
        {
            if (grandMissilePrefab == null)
            {
                Debug.LogWarning("[SpawnGrandMissiles] grandMissilePrefab is NOT assigned!");
                return;
            }

            GrandMissileType type = (GrandMissileType)Random.Range(0, 2);
            float speed = Random.Range(grandMissileData.speed / 2f, grandMissileData.speed);
            int diameter = Random.Range(2, grandMissileData.diameter);

            int direction = 0;
            if (type == GrandMissileType.Horizen)
                direction = Random.Range(1, 5);

            Vector3 spawnPosition = Vector3.zero;

            int spawnIndex = Random.Range(0, 100);
            int row = spawnIndex / 10;
            int col = spawnIndex % 10;

            if (type == GrandMissileType.Vertical)
            {
                row = AdjustGrandAxis(row, diameter);
                col = AdjustGrandAxis(col, diameter);

                spawnIndex = row * 10 + col;

                GameObject centerTile = gamePlatform.GetTile(spawnIndex);
                if (centerTile == null) continue;

                spawnPosition = centerTile.transform.position;
                spawnPosition.x -= tileX / 2;
                spawnPosition.z += tileZ / 2;
                spawnPosition.y = GlobalData.Instance.MissileDropPoint;
            }
            else
            {
                int axis = Random.Range(0, spawnPointGroup.NorthPoints.Count);
                axis = AdjustGrandAxis(axis, diameter);

                spawnPosition = spawnPointGroup.GetPointPosition(direction - 1, axis);

                switch(diameter)
                {
                    case 2: spawnPosition.y += 4f; break;
                    case 3: spawnPosition.y += 5.5f; break;
                    case 4: spawnPosition.y += 6.5f; break;
                    case 5: spawnPosition.y += 7.5f; break;
                }
            }

            GameObject missileObject = Instantiate(grandMissilePrefab);
            missileObject.transform.position = spawnPosition;

            GrandMissile grandMissile = missileObject.GetComponent<GrandMissile>();
            if (grandMissile != null)
            {
                grandMissile.SetStat(type, speed, diameter, direction);
                grandMissile.Initialize();
                grandMissile.SpawnIndex = spawnIndex;
            }

            currentGrnadMissiles.AddLast(missileObject);
        }
    }

    /// <summary>
    /// 타입별 고유 주기로 SpawnSchedule을 생성하여 큐에 삽입 (시간순 정렬)
    /// </summary>
    private void FillSpawnQueue()
    {
        List<SpawnSchedule> schedules = new List<SpawnSchedule>();
        float time = 0f;

        // Falling: waiting 기반 간격
        while (schedules.Count < MaxQueueSize)
        {
            float interval = Mathf.Round(Random.Range(fallingMissileData.waiting / 2f, fallingMissileData.waiting) * 100f) / 100f;
            float wait = Mathf.Round(Random.Range(0f, 5f) * 100f) / 100f;
            time += interval + wait;
            schedules.Add(new SpawnSchedule(time, MissileType.Falling));
        }

        // Hover: 15초 고정 간격 (레벨 10 이상)
        if (homingLoop)
        {
            float hoverTime = 15f;
            while (hoverTime <= time)
            {
                schedules.Add(new SpawnSchedule(hoverTime, MissileType.Hover));
                hoverTime += 15f;
            }
        }

        // Grand: 15~30초 랜덤 간격, 30% 확률 (레벨 25 이상)
        if (grandLoop)
        {
            float grandTime = Random.Range(15f, 30f);
            while (grandTime <= time)
            {
                if (Random.Range(0, 100) >= 70)
                    schedules.Add(new SpawnSchedule(grandTime, MissileType.Grand));
                grandTime += Random.Range(15f, 30f);
            }
        }

        // 시간순 정렬 후 큐에 삽입
        schedules.Sort((a, b) => a.Time.CompareTo(b.Time));
        spawnQueue.Clear();
        for (int i = 0; i < schedules.Count; i++)
            spawnQueue.Enqueue(schedules[i]);
    }

    /// <summary>
    /// 큐 소진 시 랜덤 타입 하나를 즉시 스폰 (사이클 전환 간격 방지)
    /// </summary>
    private void SpawnRandomOnQueueEmpty()
    {
        int roll = Random.Range(0, 3);
        switch (roll)
        {
            case 0: SpawnFallingMissiles(); break;
            case 1: if (homingLoop) SpawnHoverMissiles(); break;
            case 2: if (grandLoop) SpawnGrandMissiles(); break;
        }
    }

    /// <summary>
    /// 대형 미사일의 축 위치를 조정하여 범위를 벗어나지 않게 함
    /// </summary>
    /// <param name="axis">조정할 축 값 (0~9)</param>
    /// <param name="diameter">미사일 직경</param>
    /// <returns>조정된 축 값</returns>
    private int AdjustGrandAxis(int axis, int diameter)
    {
        float radius = diameter / 2f;

        // 왼쪽/아래 벗어남: 홀수 diameter의 0.5타일 오차 방지를 위해 올림 처리
        if (axis - radius < 0)
            return Mathf.CeilToInt(radius);
        // 오른쪽/위 벗어남
        else if (axis + radius > 9)
            return 9 - Mathf.CeilToInt(radius);

        return axis;
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// 낙하 미사일 생성 루프
    /// </summary>
    /// <returns> 코루틴 </returns>
    IEnumerator FallingMissileSpawnLoop()
    {
        // 플레이 씬 동안 계속 진행
        while (true)
        {
            // 미사일 생성 타이머 설정
            float missileTimer = Mathf.Round(Random.Range(fallingMissileData.waiting / 2f, fallingMissileData.waiting) * 100f) / 100f;
            yield return new WaitForSeconds(missileTimer);

            SpawnFallingMissiles();

            // 다음 주기 대기 시간
            float waitTimer = Mathf.Round(Random.Range(0f, 5f) * 100f) / 100f;

            yield return new WaitForSeconds(waitTimer);
        }
    }

    /// <summary>
    /// 추적 미사일 생성 루프
    /// </summary>
    /// <returns> 코루틴 </returns>
    IEnumerator HoverMissileSpawnLoop()
    {
        while (true)
        {
            SpawnHoverMissiles();
            yield return new WaitForSeconds(15f);
        }
    }

    /// <summary>
    /// 대형 미사일 생성 루프
    /// </summary>
    /// <returns>코루틴</returns>
    IEnumerator GrandMissileSpawnLoop()
    {
        while (true)
        {
            SpawnGrandMissiles();
            yield return new WaitForSeconds(Random.Range(15f, 30f));
        }
    }

    #endregion

}

