using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;


// TODO : UPDATE LELVEL을 좀 더 간소화 시킬 방법을 찾아야 할 것 같다.
// 이름을 매번 update에서 캐싱하는게 별로일수도

/// <summary>
/// 게임의 전반적인 진행사항을 다루는 싱글톤
/// </summary>
/// <remarks>
/// PlayScene일 때, 시간에 따른 난이도 조절,
/// 씬 시작 및 종료 이벤트
/// </remarks>
public class GameProgress : Singleton<GameProgress>
{

    #region Serialized Fields

    [SerializeField] private float gameTimer = 0f;

    [SerializeField] private float levelTimer = 0f;

    [SerializeField] private float currentLevel = 1f;

    [SerializeField] private int playerScore = 0;

    [SerializeField] private bool isPlayerDead = false;

    // 광고 부활과 부활 아이템의 중복 사용을 막기 위한 변수
    [SerializeField] private bool playerAlive = false;

    [SerializeField] private bool spawnXAxis = false;

    [SerializeField] private bool spawnYAxis = false;

    [SerializeField] private float soundfadeTime = 1f;

    [SerializeField] private bool spawnItem = false;

    // 부활 효과 스포트라이트 객체
    [SerializeField] private GameObject reviveSpotLight;

    // 그레이 스케일 효과 객체
    [SerializeField] private GreyScaleTrigger greyScale;
    #endregion

    #region Properties

    // 미사일이 떨어질때마다 점수 +1
    public int Score
    {
        get { return playerScore; }
        set { playerScore++; }
    }

    public bool PlayerAlive
    {
        get { return playerAlive; }
        set { playerAlive = value; }
    }

    public bool IsPlaying => !isPlayerDead;

    public float SoundFadeTime => soundfadeTime;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 게임 프레임을 고정 및 이벤트 등록
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        Application.targetFrameRate = 60;

        Player.OnPlayerDead.AddListener(EndGame);
        Player.ActiveEffect.AddListener(ActiveReviveEffect);
        StartScene.AddListener(StartProtocol);
        EndScene.AddListener(EndProtocol);

    }

    /// <summary>
    /// PlayScene일 때, 타이머를 더해주며, 5초가 지날때마다 레벨을 하나씩 증가
    /// </summary>
    /// <remarks>
    /// 레벨이 증가했을 때, 게임의 난이도를 UpdateLevel로 재조정
    /// 10 레벨 이상일 때, X축 미사일 스폰을 시작
    /// </remarks>
    void Update()
    {
        gameTimer += Time.deltaTime;

        if (SceneManager.GetActiveScene().name == GlobalData.Instance.PlayScene)
        {
            levelTimer += Time.deltaTime;
        }

        if (levelTimer >= 5f)
        {
            ++currentLevel;

            UpdateLevel();

            levelTimer = 0f;

        }
    }

    #endregion

    #region Public Methods

    public void AddCustomScore(int value) => playerScore += value;

    public void RegisterReviveLight(GameObject spotLight) => reviveSpotLight = spotLight;

    public void RegisterGreyScaleTrigger(GreyScaleTrigger greyScaleTrigger) => greyScale = greyScaleTrigger;

    /// <summary>
    /// 보상형 광고 이후 게임 재시작
    /// </summary>
    /// <remarks>
    /// 게임 오버일때 나오는 GreyScale을 다시 초기화
    /// 기존 진행 상태에 따른 스폰 재개
    /// </remarks>
    public void AdRevive()
    {
        
        GlobalData.Instance.Player.GetComponent<Player>().ActiveRevive();
        greyScale.ResetGreyScale();

        MissileSpawner.Instance.StartCoroutine("MissileSpawnLoop");

        if (currentLevel >= 10)
            MissileSpawner.Instance.StartCoroutine("XAxisMissileSpawnLoop");
        else
            spawnXAxis = false;

        if (currentLevel >= 20)
            ItemSpawner.Instance.StartCoroutine("ItemSpawnLoop");
        else
            spawnItem = false;

        Time.timeScale = 1f;
        
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 타임 스케일을 조정, PlayScene라면, 진행에 필요한 변수들을 초기화
    /// </summary>
    /// <remarks>
    /// 타이머, 점수, 레벨, Y축 미사일 스폰, 플레이어 사망 여부
    /// </remarks>
    protected override void StartProtocol()
    {
        EventSystem.current.enabled = true;

        if (Time.timeScale == 0f)
            Time.timeScale = 1f;

        if (SceneManager.GetActiveScene().name == GlobalData.Instance.PlayScene)
        {
            gameTimer = 0f;
            levelTimer = 0f;
            currentLevel = 1f;

            playerScore = 0;

            isPlayerDead = false;

            spawnXAxis = false;

            playerAlive = false;
        }
    }

    protected override void EndProtocol()
    {
        spawnXAxis = false;
        spawnItem = false;
        reviveSpotLight = null;
    }

    /// <summary>
    /// 난이도 변경 함수
    /// </summary>
    /// <remarks>
    /// 다음 최소 미사일 개수,
    /// 현재 최대 미사일 개수,
    /// 미사일 스폰 사이클,
    /// 미사일 기본 속도,
    /// X축 미사일 세팅,
    /// </remarks>
    private void UpdateLevel()
    {
        // 제곱근 방식으로 레벨 증가하도록 수정(25 레벨에 50 도달)
        int nextLimitMissileCount = Mathf.FloorToInt(Mathf.Sqrt(currentLevel * 100));

        // Y축 미사일 스폰시
        if (spawnYAxis)
        {
            nextLimitMissileCount = Mathf.FloorToInt(Mathf.Clamp(nextLimitMissileCount, 1, (float)MissileSpawner.Instance.limitMissileCount - 1));

            //최대 미사일 개수 설정
            if (nextLimitMissileCount > MissileSpawner.Instance.CurMaxMissileCount && nextLimitMissileCount < MissileSpawner.Instance.limitMissileCount)
            {
                MissileSpawner.Instance.CurMaxMissileCount = nextLimitMissileCount;
            }
            else
            {
                MissileSpawner.Instance.CurMaxMissileCount = MissileSpawner.Instance.limitMinMissileCount;
                nextLimitMissileCount = MissileSpawner.Instance.limitMissileCount;
            }

            // 최소 미사일 개수 설정
            MissileSpawner.Instance.LimitMinMissileCount = nextLimitMissileCount / 2;
        }

        // 미사일 사이클 설정
        float missileCycle = MissileSpawner.Instance.MissileCycle - 0.05f;
        missileCycle = Mathf.Clamp(missileCycle, 1f, 3f);

        MissileSpawner.Instance.MissileCycle = missileCycle;

        // 미사일 기초 속도 설정
        float nextMissileSpeed = MissileSpawner.Instance.MissileBaseSpeed - 0.05f;
        nextMissileSpeed = Mathf.Max(nextMissileSpeed, 2f);

        MissileSpawner.Instance.MissileBaseSpeed = nextMissileSpeed;

        if (spawnXAxis == false && currentLevel >= 10)
        {
            spawnXAxis = true;
            MissileSpawner.Instance.StartCoroutine("XAxisMissileSpawnLoop");
        }

        // X축 미사일 스폰 상태라면
        if (spawnXAxis)
        {
            MissileSpawner.Instance.UpdateSetting();
        }


        if (!spawnItem && currentLevel >= 20)
        {
            spawnItem = true;
            ItemSpawner.Instance.StartCoroutine("ItemSpawnLoop");
        }
    }

    /// <summary>
    /// 플레이어 사망 시 게임 종료 처리 (점수 저장 및 광고 호출)
    /// </summary>
    private void EndGame()
    {
        Time.timeScale = 0f;
        isPlayerDead = true;

        // 부활이 아닐때만 점수 기록
        if(PlayerAlive == false)
        GameData.Instance.SaveScore(playerScore);

        GoogleMobileAdsController.Instance.DisplayInterstitialAd();

    }

    private void ActiveReviveEffect()
    {
        reviveSpotLight.SetActive(true);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// 씬이 시작했을 때, 할 일들을 관리하는 이벤트
    /// </summary>
    public static UnityEvent StartScene = new UnityEvent();

    /// <summary>
    /// 씬을 종료할 때, 할 일들을 관리하는 이벤트
    /// </summary>
    public static UnityEvent EndScene = new UnityEvent();

    #endregion

}


