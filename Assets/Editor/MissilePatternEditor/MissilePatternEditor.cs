using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class MissilePatternEditor : EditorWindow
{
    #region Enums
    private enum ViewMode            { TopDown, SceneView }
    private enum EndOfPatternPolicy  { Destroy, KeepLast }           // Phase 2: 재생 종료 시 스폰 오브젝트 처리 방식
    
    #endregion

    #region Layout Constants
    private const float  ToolbarHeight      = 40f;
    private const float  PlaybarHeight      = 60f;
    private const float  TilePixelSize      = 40f;   // scale=1 기준 픽셀/타일
    private const float  MinScale           = 0.3f;
    private const float  MaxScale           = 3.0f;
    private const int    GridCols           = 10;
    private const int    GridRows           = 10;
    private const float  FullViewHalfExtent = 17f;   // Reset View 시 커버할 world 반경 (스폰포인트 ~15 + 여백)
    private const string PlatformTilePath   = "Assets/Prefabs/Platform/GrassTile.prefab";
    // 게임 MissileSpawner.CreateSpawnPoint() 와 동일한 오프셋 (플랫폼 가장자리 타일 중심에서 +10)
    private const float  SpawnOffset        = 10f;

    // 씬뷰 미사일 ghost 스폰 기준 (ADR-016)
    // GlobalData.MissileDropPoint = 50, MissileSpawner.HoverMissileSpawnLoop Y += 2.5f
    private const float FallingSpawnHeight = 50f;
    private const float HoverSpawnYOffset  = 2.5f;

    // 팔레트 레이아웃 (ADR-015)
    private const string MissilePrefabFolder = "Assets/Prefabs/Missile";
    private const float  PaletteCardSize     = 32f;   // 카드 썸네일 크기 (px)
    private const float  PaletteCardPad      = 4f;    // 카드 간 여백
    private const float  PalettePageBtnW     = 18f;   // < > 버튼 너비
    #endregion

    #region Simulation Constants
    // 배속 프리셋 — CycleSpeed()로 순환
    private static readonly float[]  SpeedPresets = { 0.25f, 0.5f, 1f, 2f };
    private static readonly string[] SpeedLabels  = { "0.25x", "0.5x", "1x", "2x" };
    // speedIndex == SpeedPresets.Length → 커스텀 모드 (float 직접 입력)
    private const int CustomSpeedIndex = 4;
    private const float DefaultTotalDuration   = 10f;
    private const float DefaultSegmentDuration = 2f;
    #endregion

    #region Colors
    private static readonly Color ViewportBackground      = new Color(0.13f, 0.13f, 0.13f);
    private static readonly Color ToolbarBackground       = new Color(0.28f, 0.18f, 0.08f);
    private static readonly Color PlaybarBackground       = new Color(0.08f, 0.22f, 0.08f);
    private static readonly Color GridLineColor           = new Color(0.32f, 0.32f, 0.32f);
    private static readonly Color TileColor               = new Color(0.19f, 0.19f, 0.19f);
    private static readonly Color BorderColor             = new Color(0.65f, 0.65f, 0.65f);
    private static readonly Color SpawnPointCardinalColor = new Color(0.35f, 0.65f, 1.00f);  // 파랑 — N/S/E/W
    private static readonly Color SpawnPointDiagonalColor = new Color(1.00f, 0.65f, 0.25f);  // 주황 — NE/NW/SE/SW
    private static readonly Color TileHoverColor           = new Color(1.00f, 1.00f, 1.00f, 0.08f);   // 반투명 흰색 오버레이
    private static readonly Color TileSelectedColor        = new Color(0.25f, 0.55f, 1.00f, 0.40f);   // 반투명 파랑 오버레이
    private static readonly Color SceneSelectionColor      = new Color(0.25f, 0.55f, 1.00f, 0.90f);   // 씬뷰 선택 마커
    private static readonly Color SpawnPointSelectedColor  = new Color(1.00f, 1.00f, 1.00f, 1.00f);   // 스폰포인트 선택 외곽선 (흰색)

    // 배치 미사일 도형 색 (ADR-016)
    private static readonly Color MissileFallingColor        = new Color(1.00f, 0.28f, 0.22f, 0.78f);  // 빨강 — Falling
    private static readonly Color MissileGrandColor          = new Color(1.00f, 0.58f, 0.12f, 0.78f);  // 주황 — Grand Vertical
    private static readonly Color MissileGrandHColor         = new Color(1.00f, 0.80f, 0.05f, 0.78f);  // 황금색 — Grand Horizontal
    private static readonly Color MissileHoverColor          = new Color(0.22f, 0.65f, 1.00f, 0.78f);  // 파랑 — Hover
    private static readonly Color MissileHoverHighlightColor = new Color(1.00f, 0.85f, 0.20f, 0.90f);  // 노랑 — Hover 하이라이트 (ADR-020)
    #endregion

    #region Top-Down Viewport State
    private Vector2 viewOffset      = Vector2.zero;
    private float   viewScale       = 1f;
    private bool    viewInitialized = false;
    #endregion

    #region Palette State
    // 팔레트 미사일 목록 — OnEnable에서 AssetDatabase 스캔으로 자동 갱신 (ADR-015)
    // Missile 컴포넌트 보유 프리팹만 포함. GrandMissileModel 등 비-스포너블 프리팹은 자동 제외.
    private readonly List<GameObject> palettePrefabs       = new List<GameObject>();
    private          int              selectedPaletteIndex = -1;   // -1 = 없음
    private          int              palettePage          = 0;
    #endregion

    #region Missile Data
    // 배치된 미사일 목록 (ADR-016/017)
    // Phase 3a: 위치·타입·파라미터 저장. 실제 스폰 연결은 Phase 3b.
    private struct PlacedMissile
    {
        public int               Id;
        public PlacedMissileType Type;
        public int               PrefabIndex;    // palettePrefabs 인덱스
        // Falling / Grand: 타일 위치
        public Vector2Int        TilePos;
        // Hover: 스폰포인트 ID ("N:3", "NE" 등)
        public string            SpawnId;
        // Grand 전용: 직경 (타일 단위 정수 — 게임 GrandMissile.diameter와 동일)
        public int               GrandDiameter;
        // Grand 전용: 이동 방향 (0=Vertical, 1=N→S, 2=S→N, 3=E→W, 4=W→E)
        public int               GrandDirection;
    }

    private readonly List<PlacedMissile> placedMissiles = new List<PlacedMissile>();
    private          int                 nextMissileId  = 0;
    #endregion

    #region Interaction State
    // ADR-018 개정 3: 통합 선택 — 레이어/액션 모드 제거, 클릭 위치 우선순위 자동 판별 (Missile > SpawnPoint > Tile)

    // 탑뷰 타일 다중 선택 — HashSet 기반 (ADR-004)
    // Ctrl+클릭: 토글, 단독 클릭: 초기화 후 단일 선택, 재클릭: 해제, 그리드 밖 클릭: 선택 유지
    // 배치된 미사일 선택 — Missile 레이어에서 사용. Id 기반 (ADR-018 개정)
    private readonly HashSet<int>        selectedMissileIds  = new HashSet<int>();
    // 겹침 리스트 — 마지막 클릭 위치의 미사일 ID (위→아래 순), 좌측 패널 표시용
    private readonly List<int>           overlapListIds      = new List<int>();
    private readonly HashSet<Vector2Int> selectedTiles       = new HashSet<Vector2Int>();
    // 탑뷰 스폰포인트 개별 선택 — "N:3", "E:0" (cardinal) / "NE", "SW" (diagonal) (ADR-004)
    // TODO (Phase 3): spawnId 키로 Dictionary<string, MissileData> 연결. 현재는 선택 상태만.
    private readonly HashSet<string>     selectedSpawnPoints = new HashSet<string>();
    private Vector2Int? hoveredTile      = null;   // 탑뷰: 마우스 호버 타일. null = 그리드 밖
    private int         hoveredMissileId = -1;    // 탑뷰: 마우스 호버 미사일 Id. -1 = 없음
    // 씬뷰 호버 — MouseMove Raycast 결과 (탑뷰의 hoveredTile·hoveredMissileId와 별도)
    private Vector2Int? sceneHoveredTile    = null;
    private string      sceneHoveredSpawnId = null;

    // 드래그 박스 선택 상태 — 탑뷰·씬뷰 공통
    // dragStartPos != null 이면 드래그 진행 중. 5px 이상 이동 시 isDragging=true.
    private Vector2? dragStartPos = null;   // MouseDown 위치 (뷰포트 픽셀)
    private Vector2  dragEndPos   = Vector2.zero;
    private bool     isDragging   = false;
    private bool     dragWasCtrl  = false;  // 드래그 시작 시점의 Ctrl 상태

    // Undo/Redo 스택 — HashSet은 직렬화 불가이므로 Unity Undo 대신 세션 내 자체 스택 사용
    // PushUndo()를 선택 변경 직전에 호출하면 Ctrl+Z/Y로 되돌릴 수 있음
    private readonly Stack<(HashSet<Vector2Int> tiles, HashSet<string> spawns)> undoStack = new Stack<(HashSet<Vector2Int>, HashSet<string>)>();
    private readonly Stack<(HashSet<Vector2Int> tiles, HashSet<string> spawns)> redoStack = new Stack<(HashSet<Vector2Int>, HashSet<string>)>();

    // Place 모드 상태 (ADR-017): 팔레트 카드 클릭 후 뷰포트 클릭으로 단일 배치하는 모드
    // inPlaceMode=true 동안 탑뷰 좌클릭 → 선택 대신 미사일 배치. Esc로 종료.
    // 타입별 유효 위치: Falling/Grand → 타일, Hover → 스폰포인트
    private bool              inPlaceMode           = false;
    private PlacedMissileType placingType           = PlacedMissileType.Falling;
    private int               placingPrefabIndex    = -1;

    // 씬뷰 타일 선택: selectedTiles (HashSet) 공유 — 탑뷰와 동일한 다중 선택 상태 유지
    #endregion

    #region Scene View State
    // 뷰 모드 (ADR-001 참조)
    private ViewMode currentMode = ViewMode.TopDown;

    // Additive 씬 기반 렌더링 (ADR-002 참조)
    // PlayScene을 강제 로드하는 대신 에디터 전용 임시 씬을 생성해 카메라·플랫폼을 배치.
    // 이펙트 테스트 시 MissileSpawner를 씬 컨텍스트 내에서 활용 가능하도록 Additive 방식 채택.
    private Scene         editorScene;
    private Camera        sceneCamera;
    private RenderTexture renderTexture;
    private bool          sceneInitialized = false;

    // 씬뷰 카메라 조작 상태
    private Vector3 sceneCamTarget   = Vector3.zero;
    private float   sceneCamDistance = 12f;
    private float   sceneCamPitch    = 60f;  // X 회전 (수직각). PlayScene 기본값과 동일 (→ ADR-003)
    private float   sceneCamYaw      = 0f;   // Y 회전 (수평 궤도)

    // 스폰포인트 큐브 머티리얼 — CleanupSceneView에서 명시적으로 해제
    private readonly List<Material> spawnMaterials = new List<Material>();

    // 씬뷰 클릭 감지 및 마커 렌더링용 룩업 테이블
    // InitSceneView / CreateSpawnPointObjects에서 채워지고 CleanupSceneView에서 클리어
    private readonly Dictionary<Vector2Int, Vector3> tileWorldPositions  = new Dictionary<Vector2Int, Vector3>();
    private readonly Dictionary<int, Vector2Int>     tileByInstanceId    = new Dictionary<int, Vector2Int>();
    private readonly Dictionary<int, string>         spawnByInstanceId   = new Dictionary<int, string>();
    private readonly Dictionary<string, Vector3>     spawnWorldPositions = new Dictionary<string, Vector3>();

    // 씬뷰 미사일 ghost 오브젝트 — missileId → GameObject (ADR-016)
    // placedMissiles와 1:1 동기화. PlaceMissile*/DeleteSelectedObjects/ApplyRightClickDelete 호출 시 갱신.
    private readonly Dictionary<int, GameObject> missileGhosts   = new Dictionary<int, GameObject>();
    private readonly List<Material>              missileMaterials = new List<Material>(); // DestroyImmediate 대상
    private Mesh discMesh; // 양면 플랫 원형 디스크 — Grand Vertical, Falling 용
    private Mesh quadMesh; // 양면 플랫 사각형 — Grand Horizontal 스트립 용

    // 씬뷰 플랫폼 실제 크기 정보 — Platform.cs와 동일하게 MeshFilter.bounds에서 읽음
    // platformOrigin = tile(row=0, col=0) 중심 위치 (MissileSpawner의 GetTile(0)에 해당)
    private float   tileXSize      = 1f;
    private float   tileZSize      = 1f;
    private float   tileSurfaceY   = 0f;   // 타일 윗면 Y (bounds.max.y) — ghost Y 오프셋 기준
    private Vector2 tileMeshCenterOffset = Vector2.zero; // 타일 메시 피벗→시각적 중심 오프셋 (X, Z)
    private Vector3 platformOrigin = Vector3.zero;
    #endregion

    #region Simulation State
    // Phase 2: dt 기반 재생 루프 — EditorApplication.update에서 타임 진행 (60fps 독립)
    // isPlaying = true일 때 currentTime이 SpeedPresets[speedIndex] 배속으로 증가.
    // 재생 종료 시 endPolicy 적용 (Phase 3에서 실제 미사일과 연결).
    private float              currentTime   = 0f;
    private float              totalDuration = DefaultTotalDuration;
    private bool               isPlaying     = false;
    private bool               loopPlayback  = false;  // true → 끝 도달 시 처음부터 재개
    private int                speedIndex    = 2;   // SpeedPresets[2] = 1x
    private float              playSpeed     = 1f;  // 실제 재생 배속 — OnEditorUpdate에서 dt에 직접 곱함
    // savedCustomSpeed: Custom 슬롯 입력값을 별도 보존.
    // playSpeed는 프리셋 순환 시 덮어쓰이지만, savedCustomSpeed는 Custom 재진입 때까지 유지된다.
    // Custom 진입 시 playSpeed = savedCustomSpeed로 복원 → 프리셋과 구별되는 배속 즉시 적용.
    private float              savedCustomSpeed             = 3f;
    // customSpeedFieldFocused: HandleKeyboardShortcuts에서 영문자 차단 시 사용.
    // GUI.GetNameOfFocusedControl()은 GUI.SetNextControlName 이후(DrawToolbar 내부)에만 유효하므로
    // DrawToolbar 렌더 직후 bool로 저장 → 다음 프레임 HandleKeyboardShortcuts에서 1프레임 지연으로 참조.
    private bool               customSpeedFieldFocused      = false;
    // pendingCustomFieldFocus: CycleSpeed()에서 Custom 진입 시 true로 설정.
    // DrawToolbar에서 감지 후 EditorGUI.FocusTextInControl 1회 호출 → 클릭 없이 바로 타이핑 가능.
    private bool               pendingCustomFieldFocus      = false;
    private double             lastEditorTime = 0;
    private EndOfPatternPolicy endPolicy     = EndOfPatternPolicy.Destroy;
    #endregion

    #region Timeline Data
    // GlobalSegment: 미사일 미선택 상태에서 추가 — 전체 미사일에 일괄 적용.
    // Phase 2: 자료구조 설계 + 타임라인 렌더링. 실제 미사일 이벤트 연결은 Phase 3.
    // TODO (Phase 3): Dictionary<int, List<MissileSegment>> missileSegments; (ADR-006 선택 레이어 연동)
    private struct GlobalSegment { public float Start; public float End; }
    private readonly List<GlobalSegment> globalSegments      = new List<GlobalSegment>();
    private          int                 selectedSegmentIndex = -1;   // -1 = 미선택
    #endregion

    #region Timeline Zoom State
    // ADR-012: 타임라인 휠 줌 — timelineZoom 배율로 가시 범위 제어
    // timelineZoom=1 → totalDuration 전체 표시 / >1 → 확대 (더 짧은 범위 표시)
    // 최대 줌 = totalDuration / 0.1 (최소 가시 0.1초 보장)
    private float timelineZoom      = 1f;   // 배율. min=1 (fit-all)
    private float timelineViewStart = 0f;   // 현재 뷰 좌측 끝 시간(초)
    #endregion

    [MenuItem("Window/Missile Pattern")]
    private static void ShowWindow() =>
        GetWindow<MissilePatternEditor>("Missile Pattern Editor");

    private void OnEnable()
    {
        wantsMouseMove = true;   // MouseMove 이벤트 수신 (탑뷰 호버 하이라이트)
        EditorApplication.update += OnEditorUpdate;   // Phase 2: dt 재생 루프
        ScanMissilePrefabs();    // Phase 3a: 팔레트 목록 초기화
        if (currentMode == ViewMode.SceneView)
            InitSceneView();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        CleanupSceneView();
    }

    private void OnGUI()
    {
        if (!viewInitialized && currentMode == ViewMode.TopDown)
        {
            ResetView();
            viewInitialized = true;
        }

        // 키보드 단축키를 컨트롤 렌더링보다 먼저 처리 — FloatField 등이 이벤트를 소비하기 전에 가로챔
        HandleKeyboardShortcuts();

        DrawViewport();
        DrawToolbar();
        DrawPlaybar();
        HandleInput();
    }

    // Phase 2: dt 기반 재생 루프 — EditorApplication.update에서 60fps 독립적으로 호출
    // isPlaying일 때만 currentTime 증가. lastEditorTime은 매 프레임 갱신해 스파이크 방지.
    private void OnEditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        if (isPlaying)
        {
            float dt = (float)(now - lastEditorTime) * playSpeed;
            currentTime = Mathf.Clamp(currentTime + dt, 0f, totalDuration);
            if (currentTime >= totalDuration)
            {
                if (loopPlayback)
                    currentTime = 0f;   // 루프: 처음으로 되감아 재생 유지
                else
                {
                    isPlaying = false;
                    // TODO (Phase 3): endPolicy에 따라 스폰된 미사일 처리 (Destroy / KeepLast)
                }
            }
            Repaint();
        }
        lastEditorTime = now;
    }

    #region Toolbar
    // 기본 레이아웃 — 4섹션 (Phase 1 스텁)
    // 미사일 뷰(갈색) | 시뮬 버튼(노랑) | 뷰 토글+리셋(중앙) | 부가 테스트(파랑)
    private const float MissileViewW = 180f;
    private const float SimCtrlW     = 180f;
    private const float ExtraTestW   = 160f;

    private static readonly Color SimBackground      = new Color(0.25f, 0.20f, 0.04f);  // 노랑 tint
    private static readonly Color ExtraTestBackground = new Color(0.05f, 0.10f, 0.26f);  // 파랑 tint
    private static readonly Color DividerColor        = new Color(0.00f, 0.00f, 0.00f, 0.40f);

    private void DrawToolbar()
    {
        float w       = position.width;
        float centerX = MissileViewW + SimCtrlW;
        float centerW = Mathf.Max(0f, w - MissileViewW - SimCtrlW - ExtraTestW);

        // ── 영역 배경 ──────────────────────────────────────────────────────
        EditorGUI.DrawRect(new Rect(0,             0, w,          ToolbarHeight), ToolbarBackground);
        EditorGUI.DrawRect(new Rect(MissileViewW,  0, SimCtrlW,   ToolbarHeight), SimBackground);
        EditorGUI.DrawRect(new Rect(w - ExtraTestW, 0, ExtraTestW, ToolbarHeight), ExtraTestBackground);

        // 섹션 구분선
        EditorGUI.DrawRect(new Rect(MissileViewW   - 1, 0, 1, ToolbarHeight), DividerColor);
        EditorGUI.DrawRect(new Rect(centerX        - 1, 0, 1, ToolbarHeight), DividerColor);
        if (centerW > 0f)
            EditorGUI.DrawRect(new Rect(w - ExtraTestW - 1, 0, 1, ToolbarHeight), DividerColor);

        // ── 미사일 팔레트 (갈색) ───────────────────────────────────────────
        DrawMissilePalette(new Rect(0, 0, MissileViewW, ToolbarHeight));

        // ── 시뮬 버튼 (노랑) ───────────────────────────────────────────────
        GUILayout.BeginArea(new Rect(MissileViewW, 0, SimCtrlW, ToolbarHeight));
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        // |< : 이전 구간 시작점으로 점프 (구간 없으면 t=0)
        if (GUILayout.Button("|<", GUILayout.Height(22), GUILayout.Width(28)))
            JumpToPrevSegmentOrStart();
        // ▶ / || : 재생 토글
        if (GUILayout.Button(isPlaying ? "||" : "▶", GUILayout.Height(22), GUILayout.Width(28)))
            TogglePlay();
        // >| : 다음 구간 시작점으로 점프 (구간 없으면 totalDuration)
        if (GUILayout.Button(">|", GUILayout.Height(22), GUILayout.Width(28)))
            JumpToNextSegmentOrEnd();
        // 배속 — 클릭으로 SpeedPresets 순환 → Custom 모드
        bool customSpeed = speedIndex == CustomSpeedIndex;
        string speedBtnLabel = customSpeed ? "Custom" : SpeedLabels[speedIndex];
        if (GUILayout.Button(speedBtnLabel, GUILayout.Height(22), GUILayout.Width(customSpeed ? 50 : 38)))
            CycleSpeed();
        // Custom 모드일 때만 float 입력 필드 표시.
        // 0·음수 방지: Mathf.Max(float.Epsilon, ...) — 0 입력 시 시뮬 완전 정지, 음수 시 역재생 방지
        if (customSpeed)
        {
            // SetNextControlName → FloatField 순서 필수.
            // 이름은 렌더 이후에만 GetNameOfFocusedControl로 조회 가능하므로
            // customSpeedFieldFocused는 FloatField 렌더 직후에 갱신한다 (HandleKeyboardShortcuts에서 1프레임 지연 참조).
            GUI.SetNextControlName("CustomSpeedField");
            float newSpeed = EditorGUILayout.FloatField(playSpeed, GUILayout.Height(20), GUILayout.Width(40));
            customSpeedFieldFocused = GUI.GetNameOfFocusedControl() == "CustomSpeedField";
            if (pendingCustomFieldFocus)
            {
                // GUI.FocusControl은 포커스만 이동하지만 FocusTextInControl은 텍스트 편집 모드까지 활성화한다.
                // Custom 진입 직후 클릭 없이 바로 타이핑 가능하게 하기 위해 사용.
                EditorGUI.FocusTextInControl("CustomSpeedField");
                pendingCustomFieldFocus = false;
            }
            if (newSpeed != playSpeed)
            {
                playSpeed        = Mathf.Max(float.Epsilon, newSpeed);
                savedCustomSpeed = playSpeed;  // savedCustomSpeed 동기화 — Custom 재진입 시 이 값을 복원
            }
            GUILayout.Label("x", GUILayout.Width(10));
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        GUILayout.EndArea();

        // ── 뷰 토글 + Reset (중앙) ─────────────────────────────────────────
        if (centerW > 0f)
        {
            GUILayout.BeginArea(new Rect(centerX, 0, centerW, ToolbarHeight));
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginChangeCheck();
            int newModeIndex = GUILayout.Toolbar(
                (int)currentMode,
                new[] { "탑뷰", "씬뷰" },
                GUILayout.Height(24), GUILayout.Width(120));
            if (EditorGUI.EndChangeCheck())
                SwitchMode((ViewMode)newModeIndex);
            GUILayout.Space(6);
            if (GUILayout.Button("Reset", GUILayout.Height(24)))
                ResetView();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ── 부가 테스트 (파랑) ─────────────────────────────────────────────
        GUILayout.BeginArea(new Rect(w - ExtraTestW, 0, ExtraTestW, ToolbarHeight));
        GUILayout.BeginHorizontal();
        GUILayout.Label("부가 테스트", EditorStyles.boldLabel,
                        GUILayout.ExpandHeight(true), GUILayout.Width(80));
        GUILayout.Label("(미구현)", EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandHeight(true));
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    // ── 팔레트 ─────────────────────────────────────────────────────────────

    // Assets/Prefabs/Missile/ 폴더를 스캔해 스포너블 프리팹만 등록 (ADR-015)
    // 필터 조건: Missile 컴포넌트 보유 + 이름이 "Model"로 끝나지 않음
    // GrandMissileModel처럼 비주얼 전용 서브 프리팹은 "Model" 접미사로 제외
    private void ScanMissilePrefabs()
    {
        palettePrefabs.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { MissilePrefabFolder });
        foreach (string guid in guids)
        {
            string     path   = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null
                && prefab.GetComponent<Missile>() != null
                && !prefab.name.EndsWith("Model"))
                palettePrefabs.Add(prefab);
        }
        selectedPaletteIndex = -1;
        palettePage          = 0;
    }

    // 팔레트 카드 렌더링 (ADR-015)
    // - 패널 너비에 맞게 열 수 자동 계산, < > 버튼으로 페이지 전환
    // - AssetPreview 썸네일이 비동기로 로드되므로, null이면 약어 텍스트 대체 후 Repaint 요청
    private void DrawMissilePalette(Rect area)
    {
        float cardStep    = PaletteCardSize + PaletteCardPad;
        float usableW     = area.width - PalettePageBtnW * 2f - PaletteCardPad;
        int   cardsPerPage = Mathf.Max(1, Mathf.FloorToInt(usableW / cardStep));
        int   pageCount    = palettePrefabs.Count == 0
                             ? 1
                             : Mathf.CeilToInt((float)palettePrefabs.Count / cardsPerPage);
        palettePage = Mathf.Clamp(palettePage, 0, Mathf.Max(0, pageCount - 1));

        int  startIdx    = palettePage * cardsPerPage;
        int  endIdx      = Mathf.Min(startIdx + cardsPerPage, palettePrefabs.Count);
        bool needRepaint = false;

        GUILayout.BeginArea(area);
        GUILayout.BeginHorizontal();

        // < 이전 페이지
        GUI.enabled = palettePage > 0;
        if (GUILayout.Button("<", GUILayout.Width(PalettePageBtnW), GUILayout.ExpandHeight(true)))
            palettePage--;
        GUI.enabled = true;

        GUILayout.Space(PaletteCardPad);

        // 카드
        for (int i = startIdx; i < endIdx; i++)
        {
            GameObject prefab  = palettePrefabs[i];
            Texture2D  preview = AssetPreview.GetAssetPreview(prefab);
            bool       selected = selectedPaletteIndex == i;

            if (preview == null) needRepaint = true;   // 썸네일 비동기 대기 중

            // 선택된 카드 파랑 tint
            Color prevColor = GUI.color;
            if (selected) GUI.color = new Color(0.5f, 0.8f, 1f, 1f);

            GUIContent content = preview != null
                ? new GUIContent(preview, prefab.name)
                : new GUIContent(prefab.name[..Mathf.Min(2, prefab.name.Length)], prefab.name);

            if (GUILayout.Button(content, GUILayout.Width(PaletteCardSize), GUILayout.Height(PaletteCardSize)))
            {
                if (selected)
                {
                    // 재클릭: 선택 해제 + Place 모드 종료
                    selectedPaletteIndex = -1;
                    inPlaceMode          = false;
                }
                else
                {
                    selectedPaletteIndex = i;
                    PlacedMissileType type    = GetTypeForPrefab(i);
                    bool              isHover = type == PlacedMissileType.Hover;
                    bool              isGrand = type == PlacedMissileType.Grand;
                    bool hasValidSelection    = isHover
                        ? selectedSpawnPoints.Count > 0
                        : isGrand
                            ? selectedTiles.Count > 0 || selectedSpawnPoints.Count > 0
                            : selectedTiles.Count > 0;

                    if (hasValidSelection)
                    {
                        // 선택된 위치에 즉시 다중 배치 (ADR-017)
                        if (isHover)
                        {
                            foreach (string spawnId in selectedSpawnPoints)
                                PlaceMissileAtSpawn(type, i, spawnId);
                        }
                        else if (isGrand)
                        {
                            foreach (Vector2Int tile in selectedTiles)
                                PlaceMissileAt(type, i, tile, 0);
                            foreach (string spawnId in selectedSpawnPoints)
                                PlaceGrandHorizontalAtSpawn(i, spawnId);
                        }
                        else
                        {
                            foreach (Vector2Int tile in selectedTiles)
                                PlaceMissileAt(type, i, tile, 0);
                        }
                    }
                    else
                    {
                        // Place 모드 진입 (ADR-017)
                        inPlaceMode        = true;
                        placingType        = type;
                        placingPrefabIndex = i;
                    }
                }
                Repaint();
            }

            GUI.color = prevColor;
            GUILayout.Space(PaletteCardPad);
        }


        GUILayout.FlexibleSpace();

        // > 다음 페이지
        GUI.enabled = palettePage < pageCount - 1;
        if (GUILayout.Button(">", GUILayout.Width(PalettePageBtnW), GUILayout.ExpandHeight(true)))
            palettePage++;
        GUI.enabled = true;

        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        if (needRepaint) Repaint();
    }
    #endregion

    #region Viewport
    private Rect ViewportRect =>
        new Rect(0, ToolbarHeight, position.width,
                 position.height - ToolbarHeight - PlaybarHeight);

    private void DrawViewport()
    {
        Rect vp = ViewportRect;
        EditorGUI.DrawRect(vp, ViewportBackground);

        if (currentMode == ViewMode.SceneView)
        {
            if (Event.current.type == EventType.Repaint)
                DrawSceneViewport(vp);
        }
        else if (Event.current.type == EventType.Repaint)
        {
            DrawGridArea(vp);
            DrawTiles(vp);
            DrawBorder(vp);
            DrawSpawnPoints(vp);
            DrawPlacedMissiles(vp);   // Phase 3a: 타입별 도형 오버레이 (ADR-016)
            DrawMissileLegend(vp);    // Phase 3a: 범례
        }

        // 드래그 박스 오버레이 — 탑뷰·씬뷰 공통 (진행 중일 때만)
        if (Event.current.type == EventType.Repaint && isDragging && dragStartPos != null)
            DrawDragBox(dragStartPos.Value, dragEndPos);

        // 오버레이 — Brush 활성 중에만 배지 표시 (ADR-017 개정 1)
        DrawInteractionModeOverlay(vp);
        // 겹침 미사일 리스트 — 클릭 위치에 2개 이상이면 오버레이 아래에 표시
        if (currentMode == ViewMode.TopDown)
            DrawMissileOverlapList(vp);

        // 커서 피드백 — Repaint 이벤트에서 AddCursorRect 호출 (ADR-020)
        // Brush 커서는 탑뷰·씬뷰 공통, Hover Link 커서는 탑뷰 전용
        if (Event.current.type == EventType.Repaint)
        {
            if (inPlaceMode)
                EditorGUIUtility.AddCursorRect(vp, MouseCursor.ArrowPlus);
            else if (currentMode == ViewMode.TopDown && (hoveredMissileId >= 0 || hoveredTile.HasValue))
                EditorGUIUtility.AddCursorRect(vp, MouseCursor.Link);
        }
    }

    // 드래그 박스 시각화 — 반투명 파랑 채우기 + 1px 외곽선 (EditorGUI.DrawRect)
    private static void DrawDragBox(Vector2 start, Vector2 end)
    {
        Rect  box     = GetBoxRect(start, end);
        Color fill    = new Color(0.25f, 0.55f, 1f, 0.08f);
        Color outline = new Color(0.25f, 0.55f, 1f, 0.65f);
        const float t = 1f;

        EditorGUI.DrawRect(box, fill);
        EditorGUI.DrawRect(new Rect(box.x,    box.y,    box.width,  t), outline);  // top
        EditorGUI.DrawRect(new Rect(box.x,    box.yMax, box.width,  t), outline);  // bottom
        EditorGUI.DrawRect(new Rect(box.x,    box.y,    t, box.height), outline);  // left
        EditorGUI.DrawRect(new Rect(box.xMax, box.y,    t, box.height + t), outline);  // right
    }

    // ── 탑뷰 렌더링 ──────────────────────────────────────────────────────────

    // 그리드 전체 영역 배경 — 타일 사이 1px 틈새에서 격자선으로 보임
    private void DrawGridArea(Rect vp)
    {
        Vector2 topLeft     = WorldToScreen(vp, new Vector2(-GridCols * 0.5f,  GridRows * 0.5f));
        Vector2 bottomRight = WorldToScreen(vp, new Vector2( GridCols * 0.5f, -GridRows * 0.5f));
        EditorGUI.DrawRect(
            new Rect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y),
            GridLineColor);
    }

    // 각 타일을 1px inset으로 채움 → GridLineColor가 격자선으로 비침
    // 호버/선택 상태는 반투명 오버레이로 위에 추가 (TileColor 위에 겹쳐 그림)
    private void DrawTiles(Rect vp)
    {
        for (int row = 0; row < GridRows; row++)
        for (int col = 0; col < GridCols; col++)
        {
            Vector2 worldMin  = TileTopLeft(col, row);
            Vector2 worldMax  = new Vector2(worldMin.x + 1f, worldMin.y - 1f);
            Vector2 screenMin = WorldToScreen(vp, worldMin);
            Vector2 screenMax = WorldToScreen(vp, worldMax);

            Rect tileRect = new Rect(screenMin.x + 1, screenMin.y + 1,
                                     screenMax.x - screenMin.x - 2,
                                     screenMax.y - screenMin.y - 2);
            EditorGUI.DrawRect(tileRect, TileColor);

            var coord = new Vector2Int(col, row);
            if (selectedTiles.Contains(coord))
                EditorGUI.DrawRect(tileRect, TileSelectedColor);
            else if (hoveredTile == coord)
                EditorGUI.DrawRect(tileRect, TileHoverColor);
        }
    }

    // 그리드 외곽 테두리
    private void DrawBorder(Rect vp)
    {
        Vector2 tl = WorldToScreen(vp, new Vector2(-GridCols * 0.5f,  GridRows * 0.5f));
        Vector2 br = WorldToScreen(vp, new Vector2( GridCols * 0.5f, -GridRows * 0.5f));
        const float t = 2f;

        EditorGUI.DrawRect(new Rect(tl.x - t, tl.y - t, br.x - tl.x + t * 2, t), BorderColor);  // top
        EditorGUI.DrawRect(new Rect(tl.x - t, br.y,     br.x - tl.x + t * 2, t), BorderColor);  // bottom
        EditorGUI.DrawRect(new Rect(tl.x - t, tl.y - t, t, br.y - tl.y + t * 2), BorderColor);  // left
        EditorGUI.DrawRect(new Rect(br.x,     tl.y - t, t, br.y - tl.y + t * 2), BorderColor);  // right
    }

    // 스폰포인트 표시
    // ※ 이 좌표계는 탑뷰 뷰포트 전용: 1 unit = 1 타일.
    //    게임 월드의 실제 타일 크기(tileXScale 등)와는 무관.
    //
    // 게임 MissileSpawner.CreateSpawnPoint() 로직을 타일 단위로 재현.
    // 각 값의 유래 (1 unit = 1 tile 기준):
    //   cardinalD = 경계 타일 중심(halfRows - 0.5f = 4.5) + SpawnOffset(10) = 14.5
    //             → 카디널은 타일 중심에서 바로 SpawnOffset을 더함 (모서리 보정 없음)
    //   diagX     = 경계 타일 중심(4.5) - 왼쪽 모서리 보정(0.5) + SpawnOffset(10) = 14
    //   diagZ     = 경계 타일 중심(4.5) + 위쪽 모서리 보정(0.5) + SpawnOffset(10) = 15
    //             → 대각선은 코너 타일의 좌상단 모서리를 기준으로 SpawnOffset을 더함
    private void DrawSpawnPoints(Rect vp)
    {
        float halfCols  = GridCols * 0.5f;                       // 5
        float halfRows  = GridRows * 0.5f;                       // 5
        float cardinalD = halfRows - 0.5f + SpawnOffset;         // 14.5  (N/S Y, E/W X)
        float diagX     = halfCols - 1f   + SpawnOffset;         // 14    (NE/SE X)
        float diagZ     = halfRows         + SpawnOffset;         // 15    (N 대각선 Z)
        float diagZSouth= halfRows - 1f   + SpawnOffset;         // 14    (S 대각선 Z, 남쪽 타일 중심 보정)

        for (int i = 0; i < GridCols; i++)
        {
            DrawSpawnDot(vp, new Vector2(-halfCols + i + 0.5f,  cardinalD), SpawnPointCardinalColor, 5f, $"N:{i}");  // North
            DrawSpawnDot(vp, new Vector2(-halfCols + i + 0.5f, -cardinalD), SpawnPointCardinalColor, 5f, $"S:{i}");  // South
            DrawSpawnDot(vp, new Vector2( cardinalD,  halfRows - i - 0.5f), SpawnPointCardinalColor, 5f, $"E:{i}");  // East
            DrawSpawnDot(vp, new Vector2(-cardinalD,  halfRows - i - 0.5f), SpawnPointCardinalColor, 5f, $"W:{i}");  // West
        }

        DrawSpawnDot(vp, new Vector2( diagX,  diagZ),      SpawnPointDiagonalColor, 7f, "NE");
        DrawSpawnDot(vp, new Vector2(-diagZ,  diagZ),      SpawnPointDiagonalColor, 7f, "NW");
        DrawSpawnDot(vp, new Vector2( diagX, -diagZSouth), SpawnPointDiagonalColor, 7f, "SE");
        DrawSpawnDot(vp, new Vector2(-diagZ, -diagZSouth), SpawnPointDiagonalColor, 7f, "SW");

        DrawWorldLabel(vp, new Vector2(-0.5f,  diagZ + 1f), "N");
        DrawWorldLabel(vp, new Vector2(-0.5f, -diagZ - 1f), "S");
        DrawWorldLabel(vp, new Vector2( diagZ + 1f,  0.5f), "E");
        DrawWorldLabel(vp, new Vector2(-diagZ - 1f,  0.5f), "W");
    }

    // 선택 상태에 따라 외곽선 여부를 결정하는 스폰포인트 dot 렌더러
    // TODO (Phase 3): id에 미사일이 배치됐으면 색상/크기 변경 또는 미사일 아이콘 오버레이 추가.
    private void DrawSpawnDot(Rect vp, Vector2 worldPos, Color color, float size, string id)
    {
        if (selectedSpawnPoints.Contains(id))
            DrawDotOutlined(vp, worldPos, size, color);
        else
            DrawDot(vp, worldPos, color, size);
    }

    // 흰색 외곽(dotSize+4px) → 원래 색 내부(dotSize) 순서로 그려 외곽선 효과
    private void DrawDotOutlined(Rect vp, Vector2 worldPos, float dotSize, Color innerColor)
    {
        float outlined = dotSize + 4f;
        Vector2 s     = WorldToScreen(vp, worldPos);
        Rect    outer = new Rect(s.x - outlined * 0.5f, s.y - outlined * 0.5f, outlined, outlined);
        Rect    inner = new Rect(s.x - dotSize  * 0.5f, s.y - dotSize  * 0.5f, dotSize,  dotSize);
        if (!vp.Overlaps(outer)) return;
        EditorGUI.DrawRect(outer, SpawnPointSelectedColor);
        EditorGUI.DrawRect(inner, innerColor);
    }

    // 뷰포트 안에 있을 때만 dot 그림 (off-screen 스킵)
    private void DrawDot(Rect vp, Vector2 worldPos, Color color, float size = 5f)
    {
        Vector2 s   = WorldToScreen(vp, worldPos);
        Rect    dot = new Rect(s.x - size * 0.5f, s.y - size * 0.5f, size, size);
        if (vp.Overlaps(dot))
            EditorGUI.DrawRect(dot, color);
    }

    // world 좌표 기준 레이블 (뷰포트 안에서만)
    private void DrawWorldLabel(Rect vp, Vector2 worldPos, string text)
    {
        Vector2 s         = WorldToScreen(vp, worldPos);
        Rect    labelRect = new Rect(s.x - 10f, s.y - 8f, 20f, 16f);
        if (vp.Overlaps(labelRect))
            GUI.Label(labelRect, text, EditorStyles.centeredGreyMiniLabel);
    }

    // ── 탑뷰 미사일 시각화 (ADR-016) ─────────────────────────────────────────

    // spawnId → 탑뷰 world 좌표 변환 (DrawSpawnPoints와 동일한 상수 사용)
    private static Vector2 GetSpawnPointWorldPos(string spawnId)
    {
        float halfCols   = GridCols * 0.5f;
        float halfRows   = GridRows * 0.5f;
        float cardinalD  = halfRows - 0.5f + SpawnOffset;
        float diagX      = halfCols - 1f   + SpawnOffset;
        float diagZ      = halfRows         + SpawnOffset;
        float diagZSouth = halfRows - 1f   + SpawnOffset;

        if (spawnId == "NE") return new Vector2( diagX,  diagZ);
        if (spawnId == "NW") return new Vector2(-diagZ,  diagZ);
        if (spawnId == "SE") return new Vector2( diagX, -diagZSouth);
        if (spawnId == "SW") return new Vector2(-diagZ, -diagZSouth);

        if (spawnId.StartsWith("N:") && int.TryParse(spawnId[2..], out int ni))
            return new Vector2(-halfCols + ni + 0.5f,  cardinalD);
        if (spawnId.StartsWith("S:") && int.TryParse(spawnId[2..], out int si))
            return new Vector2(-halfCols + si + 0.5f, -cardinalD);
        if (spawnId.StartsWith("E:") && int.TryParse(spawnId[2..], out int ei))
            return new Vector2( cardinalD,  halfRows - ei - 0.5f);
        if (spawnId.StartsWith("W:") && int.TryParse(spawnId[2..], out int wi))
            return new Vector2(-cardinalD,  halfRows - wi - 0.5f);

        return Vector2.zero;
    }

    // Hover 스폰 방향 → 타원 반축 (rx=가로, ry=세로, world 단위)
    // N/S 스폰: 미사일이 남북 방향으로 진입 → 세로로 긴 타원
    // E/W 스폰: 미사일이 동서 방향으로 진입 → 가로로 긴 타원
    private static (float rx, float ry) GetHoverEllipseAxes(string spawnId)
    {
        if (spawnId.StartsWith("N:") || spawnId.StartsWith("S:")) return (0.35f, 0.70f);
        if (spawnId.StartsWith("E:") || spawnId.StartsWith("W:")) return (0.70f, 0.35f);
        return (0.50f, 0.50f);  // 대각선: 원형 근사
    }

    // Handles.DrawAAConvexPolygon으로 타원 근사 (segments개 꼭짓점)
    private static void DrawFilledEllipse(Vector2 center, float rx, float ry, int segments = 24)
    {
        Vector3[] pts = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            pts[i] = new Vector3(center.x + Mathf.Cos(angle) * rx,
                                 center.y + Mathf.Sin(angle) * ry, 0f);
        }
        Handles.DrawAAConvexPolygon(pts);
    }

    // 배치된 미사일 도형 오버레이 + 겹침 배지 렌더 (ADR-016)
    // Falling: 원(빨강), Grand: 직경 비례 원(주황), Hover: 방향 타원(파랑)
    // 한 위치에 2개 이상이면 오른쪽 상단에 [N] 배지 표시
    private void DrawPlacedMissiles(Rect vp)
    {
        if (placedMissiles.Count == 0) return;

        // 타일/스폰 겹침 카운트
        var tileCount  = new Dictionary<Vector2Int, int>();
        var spawnCount = new Dictionary<string, int>();
        foreach (var m in placedMissiles)
        {
            if (m.Type != PlacedMissileType.Hover)
            {
                tileCount.TryGetValue(m.TilePos, out int c);
                tileCount[m.TilePos] = c + 1;
            }
            else
            {
                spawnCount.TryGetValue(m.SpawnId, out int c);
                spawnCount[m.SpawnId] = c + 1;
            }
        }

        // 도형 렌더 순서: Grand Horizontal → Grand Vertical → Hover → Falling
        // Horizontal이 가장 먼저(배경) 그려져 나머지 도형이 위에 선명하게 표시됨
        Color oldHandlesColor = Handles.color;

        // Pass 1 — Grand Horizontal 스트립 (최배경)
        // DrawSolidRectangleWithOutline의 faceColor는 Handles.color와 곱해지므로 white로 초기화
        Handles.color = Color.white;
        foreach (var m in placedMissiles)
        {
            if (m.Type != PlacedMissileType.Grand || m.GrandDirection == 0) continue;
            float halfD    = (m.GrandDiameter > 0 ? m.GrandDiameter : 2) * 0.5f;
            var   fillColor = new Color(MissileGrandHColor.r, MissileGrandHColor.g, MissileGrandHColor.b, 0.75f);
            float ap       = TilePixelSize * viewScale * 0.38f;  // 화살표 크기
            if (m.GrandDirection <= 2)
            {
                // N↔S 방향 — 세로 스트립
                float worldX = TileCenter(m.TilePos).x;
                Handles.DrawSolidRectangleWithOutline(new Vector3[]
                {
                    ToV3(WorldToScreen(vp, new Vector2(worldX - halfD, -GridRows * 0.5f))),
                    ToV3(WorldToScreen(vp, new Vector2(worldX + halfD, -GridRows * 0.5f))),
                    ToV3(WorldToScreen(vp, new Vector2(worldX + halfD,  GridRows * 0.5f))),
                    ToV3(WorldToScreen(vp, new Vector2(worldX - halfD,  GridRows * 0.5f))),
                }, fillColor, Color.clear);
                // 진입 엣지에 방향 화살표: N→S=위쪽 엣지에서 아래방향, S→N=아래 엣지에서 위방향
                Handles.color = new Color(1f, 1f, 1f, 0.85f);
                if (m.GrandDirection == 1)  // N→S: 북쪽(위) 진입 → 아래 방향(screen +Y)
                    DrawTopViewArrow(vp, new Vector2(worldX,  GridRows * 0.5f), new Vector2( 0f,  1f), ap);
                else                        // S→N: 남쪽(아래) 진입 → 위 방향(screen -Y)
                    DrawTopViewArrow(vp, new Vector2(worldX, -GridRows * 0.5f), new Vector2( 0f, -1f), ap);
                Handles.color = Color.white;
            }
            else
            {
                // E↔W 방향 — 가로 스트립
                float worldY = TileCenter(m.TilePos).y;
                Handles.DrawSolidRectangleWithOutline(new Vector3[]
                {
                    ToV3(WorldToScreen(vp, new Vector2(-GridCols * 0.5f, worldY - halfD))),
                    ToV3(WorldToScreen(vp, new Vector2( GridCols * 0.5f, worldY - halfD))),
                    ToV3(WorldToScreen(vp, new Vector2( GridCols * 0.5f, worldY + halfD))),
                    ToV3(WorldToScreen(vp, new Vector2(-GridCols * 0.5f, worldY + halfD))),
                }, fillColor, Color.clear);
                // 진입 엣지에 방향 화살표: E→W=오른쪽 엣지에서 왼방향, W→E=왼쪽 엣지에서 오른방향
                Handles.color = new Color(1f, 1f, 1f, 0.85f);
                if (m.GrandDirection == 3)  // E→W: 동쪽(오른) 진입 → 왼 방향(screen -X)
                    DrawTopViewArrow(vp, new Vector2( GridCols * 0.5f, worldY), new Vector2(-1f,  0f), ap);
                else                        // W→E: 서쪽(왼) 진입 → 오른 방향(screen +X)
                    DrawTopViewArrow(vp, new Vector2(-GridCols * 0.5f, worldY), new Vector2( 1f,  0f), ap);
                Handles.color = Color.white;
            }
        }

        // Pass 2 — Grand Vertical 디스크
        Handles.color = MissileGrandColor;
        foreach (var m in placedMissiles)
        {
            if (m.Type != PlacedMissileType.Grand || m.GrandDirection != 0) continue;
            float   halfD = (m.GrandDiameter > 0 ? m.GrandDiameter : 2) * 0.5f;
            Vector2 c     = WorldToScreen(vp, TileCenter(m.TilePos));
            float   scale = TilePixelSize * viewScale;
            Handles.DrawSolidDisc(new Vector3(c.x, c.y, 0), Vector3.forward, halfD * scale);
        }

        Handles.color = MissileHoverColor;
        foreach (var m in placedMissiles)
        {
            if (m.Type != PlacedMissileType.Hover) continue;
            Vector2 c = WorldToScreen(vp, GetSpawnPointWorldPos(m.SpawnId));
            var (rx, ry) = GetHoverEllipseAxes(m.SpawnId);
            DrawFilledEllipse(c, rx * TilePixelSize * viewScale,
                                 ry * TilePixelSize * viewScale);
        }

        Handles.color = MissileFallingColor;
        foreach (var m in placedMissiles)
        {
            if (m.Type != PlacedMissileType.Falling) continue;
            Vector2 c  = WorldToScreen(vp, TileCenter(m.TilePos));
            float   r  = 0.45f * TilePixelSize * viewScale;
            // 링 + 중심점: 채워진 디스크 대신 외곽 링으로 표시해 Grand 스트립 색상 가림 방지
            Handles.DrawWireDisc(new Vector3(c.x, c.y, 0), Vector3.forward, r);
            Handles.DrawSolidDisc(new Vector3(c.x, c.y, 0), Vector3.forward, r * 0.25f);
        }

        Handles.color = oldHandlesColor;

        // hover 하이라이트 — 마우스 아래 미사일 노랑 윤곽선 (ADR-020)
        if (hoveredMissileId >= 0)
        {
            int mIdx = placedMissiles.FindIndex(m => m.Id == hoveredMissileId);
            if (mIdx >= 0)
            {
                Color oldColor = Handles.color;
                Handles.color = MissileHoverHighlightColor;
                var hm = placedMissiles[mIdx];
                float scale = TilePixelSize * viewScale;
                if (hm.Type == PlacedMissileType.Hover)
                {
                    Vector2 c = WorldToScreen(vp, GetSpawnPointWorldPos(hm.SpawnId));
                    var (ax, ay) = GetHoverEllipseAxes(hm.SpawnId);
                    const int Segs = 24;
                    var pts = new Vector3[Segs + 1];
                    float rx2 = ax * scale + 3f, ry2 = ay * scale + 3f;
                    for (int k = 0; k <= Segs; k++)
                    {
                        float a = k * Mathf.PI * 2f / Segs;
                        pts[k] = new Vector3(c.x + Mathf.Cos(a) * rx2, c.y + Mathf.Sin(a) * ry2, 0f);
                    }
                    Handles.DrawPolyLine(pts);
                }
                else if (hm.Type == PlacedMissileType.Grand && hm.GrandDirection != 0)
                {
                    float halfD = (hm.GrandDiameter > 0 ? hm.GrandDiameter : 2) * 0.5f;
                    Vector3[] pts;
                    if (hm.GrandDirection <= 2)
                    {
                        float wx = TileCenter(hm.TilePos).x;
                        pts = new Vector3[]
                        {
                            ToV3(WorldToScreen(vp, new Vector2(wx - halfD, -GridRows * 0.5f))),
                            ToV3(WorldToScreen(vp, new Vector2(wx + halfD, -GridRows * 0.5f))),
                            ToV3(WorldToScreen(vp, new Vector2(wx + halfD,  GridRows * 0.5f))),
                            ToV3(WorldToScreen(vp, new Vector2(wx - halfD,  GridRows * 0.5f))),
                            ToV3(WorldToScreen(vp, new Vector2(wx - halfD, -GridRows * 0.5f))),
                        };
                    }
                    else
                    {
                        float wy = TileCenter(hm.TilePos).y;
                        pts = new Vector3[]
                        {
                            ToV3(WorldToScreen(vp, new Vector2(-GridCols * 0.5f, wy - halfD))),
                            ToV3(WorldToScreen(vp, new Vector2( GridCols * 0.5f, wy - halfD))),
                            ToV3(WorldToScreen(vp, new Vector2( GridCols * 0.5f, wy + halfD))),
                            ToV3(WorldToScreen(vp, new Vector2(-GridCols * 0.5f, wy + halfD))),
                            ToV3(WorldToScreen(vp, new Vector2(-GridCols * 0.5f, wy - halfD))),
                        };
                    }
                    Handles.DrawAAPolyLine(2f, pts);
                }
                else
                {
                    Vector2 c = WorldToScreen(vp, TileCenter(hm.TilePos));
                    float r = (hm.Type == PlacedMissileType.Grand
                        ? hm.GrandDiameter / 2f * scale : 0.45f * scale) + 3f;
                    Handles.DrawWireDisc(new Vector3(c.x, c.y, 0f), Vector3.forward, r);
                }
                Handles.color = oldColor;
            }
        }

        // 선택 하이라이트 — 선택된 미사일 둘레에 흰 윤곽선 (ADR-018 개정 3)
        if (selectedMissileIds.Count > 0)
        {
            Color oldColor = Handles.color;
            Handles.color = Color.white;
            foreach (var m in placedMissiles)
            {
                if (!selectedMissileIds.Contains(m.Id)) continue;
                float scale = TilePixelSize * viewScale;
                if (m.Type == PlacedMissileType.Hover)
                {
                    Vector2 c = WorldToScreen(vp, GetSpawnPointWorldPos(m.SpawnId));
                    var (ax, ay) = GetHoverEllipseAxes(m.SpawnId);
                    const int Segs = 24;
                    var pts = new Vector3[Segs + 1];
                    float rx2 = ax * scale + 3f;
                    float ry2 = ay * scale + 3f;
                    for (int k = 0; k <= Segs; k++)
                    {
                        float a = k * Mathf.PI * 2f / Segs;
                        pts[k] = new Vector3(c.x + Mathf.Cos(a) * rx2, c.y + Mathf.Sin(a) * ry2, 0f);
                    }
                    Handles.DrawPolyLine(pts);
                }
                else if (m.Type == PlacedMissileType.Grand && m.GrandDirection != 0)
                {
                    float halfD = (m.GrandDiameter > 0 ? m.GrandDiameter : 2) * 0.5f;
                    Vector3[] pts;
                    if (m.GrandDirection <= 2)
                    {
                        float wx = TileCenter(m.TilePos).x;
                        pts = new Vector3[]
                        {
                            ToV3(WorldToScreen(vp, new Vector2(wx - halfD, -GridRows * 0.5f))),
                            ToV3(WorldToScreen(vp, new Vector2(wx + halfD, -GridRows * 0.5f))),
                            ToV3(WorldToScreen(vp, new Vector2(wx + halfD,  GridRows * 0.5f))),
                            ToV3(WorldToScreen(vp, new Vector2(wx - halfD,  GridRows * 0.5f))),
                            ToV3(WorldToScreen(vp, new Vector2(wx - halfD, -GridRows * 0.5f))),
                        };
                    }
                    else
                    {
                        float wy = TileCenter(m.TilePos).y;
                        pts = new Vector3[]
                        {
                            ToV3(WorldToScreen(vp, new Vector2(-GridCols * 0.5f, wy - halfD))),
                            ToV3(WorldToScreen(vp, new Vector2( GridCols * 0.5f, wy - halfD))),
                            ToV3(WorldToScreen(vp, new Vector2( GridCols * 0.5f, wy + halfD))),
                            ToV3(WorldToScreen(vp, new Vector2(-GridCols * 0.5f, wy + halfD))),
                            ToV3(WorldToScreen(vp, new Vector2(-GridCols * 0.5f, wy - halfD))),
                        };
                    }
                    Handles.DrawAAPolyLine(2f, pts);
                }
                else
                {
                    Vector2 c = WorldToScreen(vp, TileCenter(m.TilePos));
                    float r = (m.Type == PlacedMissileType.Grand
                        ? m.GrandDiameter / 2f * scale : 0.45f * scale) + 3f;
                    Handles.DrawWireDisc(new Vector3(c.x, c.y, 0f), Vector3.forward, r);
                }
            }
            Handles.color = oldColor;
        }

        // 겹침 배지 ([N] 레이블)
        foreach (var kv in tileCount)
        {
            if (kv.Value <= 1) continue;
            Vector2 c    = WorldToScreen(vp, TileCenter(kv.Key));
            float   half = 0.45f * TilePixelSize * viewScale;
            GUI.Label(new Rect(c.x + half * 0.3f, c.y - half - 12f, 22f, 14f),
                      $"[{kv.Value}]", EditorStyles.miniLabel);
        }
        foreach (var kv in spawnCount)
        {
            if (kv.Value <= 1) continue;
            Vector2 s = WorldToScreen(vp, GetSpawnPointWorldPos(kv.Key));
            GUI.Label(new Rect(s.x + 6f, s.y - 18f, 22f, 14f),
                      $"[{kv.Value}]", EditorStyles.miniLabel);
        }
    }

    // 범례: 뷰포트 좌하단 고정, 타입-색상 대응 표시 (ADR-016)
    private void DrawMissileLegend(Rect vp)
    {
        if (placedMissiles.Count == 0) return;  // 배치된 미사일이 없으면 숨김

        const float LegW = 116f;
        const float LegH = 74f;
        const float Pad  = 6f;
        Rect bg = new Rect(vp.x + Pad, vp.yMax - LegH - Pad, LegW, LegH);
        EditorGUI.DrawRect(bg, new Color(0f, 0f, 0f, 0.55f));

        float x = bg.x + 5f;
        float y = bg.y + 4f;
        DrawLegendRow(x, y,      MissileFallingColor, "Falling");
        DrawLegendRow(x, y + 18, MissileGrandColor,   "Grand ↓");
        DrawLegendRow(x, y + 36, MissileGrandHColor,  "Grand →");
        DrawLegendRow(x, y + 54, MissileHoverColor,   "Hover");
    }

    private static void DrawLegendRow(float x, float y, Color color, string label)
    {
        EditorGUI.DrawRect(new Rect(x, y + 2f, 10f, 10f), color);
        GUI.Label(new Rect(x + 14f, y, 80f, 14f), label, EditorStyles.miniLabel);
    }

    // 탑뷰 방향 화살표 — 스크린 좌표 기반 채워진 삼각형 (Handles.color 적용됨)
    // worldCenter: 월드 좌표, screenDir: 스크린 기준 방향 (정규화), sizePx: 화살표 크기(픽셀)
    private void DrawTopViewArrow(Rect vp, Vector2 worldCenter, Vector2 screenDir, float sizePx)
    {
        Vector2 s    = WorldToScreen(vp, worldCenter);
        Vector2 perp = new Vector2(-screenDir.y, screenDir.x);
        var tip   = new Vector3(s.x + screenDir.x * sizePx,                              s.y + screenDir.y * sizePx,  0f);
        var baseL = new Vector3(s.x - screenDir.x * sizePx * 0.4f + perp.x * sizePx * 0.6f,
                                s.y - screenDir.y * sizePx * 0.4f + perp.y * sizePx * 0.6f, 0f);
        var baseR = new Vector3(s.x - screenDir.x * sizePx * 0.4f - perp.x * sizePx * 0.6f,
                                s.y - screenDir.y * sizePx * 0.4f - perp.y * sizePx * 0.6f, 0f);
        Handles.DrawAAConvexPolygon(tip, baseL, baseR);
    }

    // 겹침 미사일 리스트 패널 — 좌측 오버레이 아래에 고정 (클릭 위치에 2개 이상일 때만 표시)
    private void DrawMissileOverlapList(Rect vp)
    {
        if (overlapListIds.Count <= 1) return;

        // 유효한 ID만 필터 (삭제된 미사일 제거)
        overlapListIds.RemoveAll(id => placedMissiles.FindIndex(m => m.Id == id) < 0);
        if (overlapListIds.Count <= 1) return;

        const float ItemH   = 20f;
        const float ListW   = 120f;
        const float SwatchW =  8f;
        const float Margin  =  8f;

        // Brush 배지 패널 아래에 배치 (비활성 시 상단 여백만)
        float overlayH = inPlaceMode ? (32f + 4f * 2f) : 0f;
        float listX = vp.x + Margin;
        float listY = vp.y + Margin + overlayH + 4f;
        float listH = overlapListIds.Count * ItemH + 4f;

        EditorGUI.DrawRect(new Rect(listX, listY, ListW, listH), new Color(0f, 0f, 0f, 0.72f));

        Event e = Event.current;
        for (int i = 0; i < overlapListIds.Count; i++)
        {
            int id   = overlapListIds[i];
            int mIdx = placedMissiles.FindIndex(m => m.Id == id);
            if (mIdx < 0) continue;
            var m = placedMissiles[mIdx];

            var rowRect = new Rect(listX, listY + 2f + i * ItemH, ListW, ItemH);

            // 선택된 항목 배경 하이라이트
            if (selectedMissileIds.Contains(id))
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.15f));

            // 클릭 → 선택
            if (e.type == EventType.MouseDown && e.button == 0 && rowRect.Contains(e.mousePosition))
            {
                selectedMissileIds.Clear();
                selectedMissileIds.Add(id);
                e.Use();
                Repaint();
            }

            // 색상 스와치 — Grand Horizontal은 황금색으로 구분
            Color typeColor = m.Type switch
            {
                PlacedMissileType.Falling => MissileFallingColor,
                PlacedMissileType.Grand   => m.GrandDirection != 0 ? MissileGrandHColor : MissileGrandColor,
                PlacedMissileType.Hover   => MissileHoverColor,
                _                         => Color.white
            };
            EditorGUI.DrawRect(new Rect(listX + 4f, rowRect.y + 6f, SwatchW, 8f), typeColor);

            // 이름 레이블 (타입 + 위치 순서 번호)
            GUI.Label(new Rect(listX + 4f + SwatchW + 4f, rowRect.y, ListW - SwatchW - 12f, ItemH),
                      $"{m.Type} {i + 1}", EditorStyles.miniLabel);
        }
    }

    // ── 미사일 배치 (ADR-017) ─────────────────────────────────────────────────

    // 팔레트 프리팹 인덱스 → 미사일 타입
    // HoverMissile → Hover, GrandMissile → Grand, 그 외 → Falling
    private PlacedMissileType GetTypeForPrefab(int prefabIndex)
    {
        if (prefabIndex < 0 || prefabIndex >= palettePrefabs.Count)
            return PlacedMissileType.Falling;
        string name = palettePrefabs[prefabIndex].name;
        if (name.Contains("Grand"))  return PlacedMissileType.Grand;
        if (name.Contains("Hover")) return PlacedMissileType.Hover;
        return PlacedMissileType.Falling;
    }

    // 타일에 미사일 배치 — Falling / Grand 전용 (ADR-017)
    // Grand 기본 직경은 4f (스탯 편집 팝업 구현 전까지 고정)
    private void PlaceMissileAt(PlacedMissileType type, int prefabIndex, Vector2Int tile, int grandDirection = 0)
    {
        var m = new PlacedMissile
        {
            Id             = nextMissileId++,
            Type           = type,
            PrefabIndex    = prefabIndex,
            TilePos        = tile,
            GrandDiameter  = type == PlacedMissileType.Grand ? 2 : 0,
            GrandDirection = type == PlacedMissileType.Grand ? grandDirection : 0,
        };
        placedMissiles.Add(m);
        SpawnMissileGhost(m);
        Repaint();
    }

    // Grand Horizontal 배치 — 스폰포인트에서 방향 자동 결정 (cardinal만 유효, ADR-021)
    // "N:X"→dir1(N→S), "S:X"→dir2(S→N), "E:X"→dir3(E→W), "W:X"→dir4(W→E)
    // TilePos: N/S는 X=열 인덱스, E/W는 Y=행 인덱스 (렌더링 strip 위치)
    private void PlaceGrandHorizontalAtSpawn(int prefabIndex, string spawnId)
    {
        if (spawnId.Length < 3 || spawnId[1] != ':') return;  // 대각선 스폰포인트 — 무시
        if (!int.TryParse(spawnId[2..], out int lineIndex))    return;

        int dir = spawnId[0] switch { 'N' => 1, 'S' => 2, 'E' => 3, 'W' => 4, _ => 0 };
        if (dir == 0) return;

        // N→S / S→N: 열(TilePos.x) 기준 세로 strip, E→W / W→E: 행(TilePos.y) 기준 가로 strip
        Vector2Int tilePos = (dir <= 2)
            ? new Vector2Int(lineIndex, GridRows / 2)
            : new Vector2Int(GridCols / 2, lineIndex);

        PlaceMissileAt(PlacedMissileType.Grand, prefabIndex, tilePos, dir);
    }

    // 스폰포인트에 미사일 배치 — Hover 전용 (ADR-017)
    private void PlaceMissileAtSpawn(PlacedMissileType type, int prefabIndex, string spawnId)
    {
        var m = new PlacedMissile
        {
            Id          = nextMissileId++,
            Type        = type,
            PrefabIndex = prefabIndex,
            SpawnId     = spawnId,
        };
        placedMissiles.Add(m);
        SpawnMissileGhost(m);
        Repaint();
    }

    // 타일에 배치된 미사일 전체 제거 (Falling/Grand) — Erase 모드용 (ADR-017)
    private void RemoveMissilesAtTile(Vector2Int tile)
    {
        placedMissiles.RemoveAll(m => m.Type != PlacedMissileType.Hover && m.TilePos == tile);
    }

    // 스폰포인트에 배치된 미사일 전체 제거 (Hover) — Erase 모드용 (ADR-017)
    private void RemoveMissilesAtSpawn(string spawnId)
    {
        placedMissiles.RemoveAll(m => m.Type == PlacedMissileType.Hover && m.SpawnId == spawnId);
    }

    // 탑뷰 스크린 좌표에서 가장 위에 있는 미사일 히트 판정 (ADR-018 개정)
    // Falling/Grand Vertical: 원 방정식, Hover: 타원 방정식, Grand Horizontal: 스트립 사각형 — 스크린 픽셀 기준
    private bool TryHitMissileTopDown(Rect vp, Vector2 mousePos, out int hitId)
    {
        hitId = -1;
        float scale = TilePixelSize * viewScale;

        // 역순 순회 — 나중에 그려진(위에 쌓인) 미사일 우선 선택
        for (int i = placedMissiles.Count - 1; i >= 0; i--)
        {
            var m = placedMissiles[i];
            Vector2 center;
            float   rx, ry;

            if (m.Type == PlacedMissileType.Hover)
            {
                center    = WorldToScreen(vp, GetSpawnPointWorldPos(m.SpawnId));
                var (ax, ay) = GetHoverEllipseAxes(m.SpawnId);
                rx = ax * scale;
                ry = ay * scale;
            }
            else if (m.Type == PlacedMissileType.Grand && m.GrandDirection != 0)
            {
                // Horizontal strip — 렌더링과 동일한 스트립 영역을 월드 좌표로 판정
                Vector2 w     = ScreenToWorld(vp, mousePos);
                float   halfD = (m.GrandDiameter > 0 ? m.GrandDiameter : 2) * 0.5f;
                Vector2 tc    = TileCenter(m.TilePos);
                bool    hit   = m.GrandDirection <= 2
                    ? Mathf.Abs(w.x - tc.x) <= halfD && w.y >= -GridRows * 0.5f && w.y <= GridRows * 0.5f
                    : Mathf.Abs(w.y - tc.y) <= halfD && w.x >= -GridCols * 0.5f && w.x <= GridCols * 0.5f;
                if (hit) { hitId = m.Id; return true; }
                continue;
            }
            else
            {
                center = WorldToScreen(vp, TileCenter(m.TilePos));
                float r = m.Type == PlacedMissileType.Grand
                    ? m.GrandDiameter / 2f * scale
                    : 0.45f * scale;
                rx = ry = r;
            }

            float dx = mousePos.x - center.x;
            float dy = mousePos.y - center.y;
            if (rx > 0f && ry > 0f && (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1f)
            {
                hitId = m.Id;
                return true;
            }
        }
        return false;
    }

    // 클릭 위치의 겹침 리스트 갱신 — 2개 이상일 때만 채움, 아니면 비움
    private void RefreshOverlapList(Rect vp, Vector2 mousePos)
    {
        var hits = GetAllHitsTopDown(vp, mousePos);
        overlapListIds.Clear();
        if (hits.Count >= 2)
            overlapListIds.AddRange(hits);
    }

    // 클릭 위치에 히트하는 모든 미사일 ID를 위에서 아래 순으로 반환 (겹침 순환 선택용)
    private List<int> GetAllHitsTopDown(Rect vp, Vector2 mousePos)
    {
        var result = new List<int>();
        float scale = TilePixelSize * viewScale;

        for (int i = placedMissiles.Count - 1; i >= 0; i--)
        {
            var m = placedMissiles[i];
            Vector2 center;
            float   rx, ry;

            if (m.Type == PlacedMissileType.Hover)
            {
                center = WorldToScreen(vp, GetSpawnPointWorldPos(m.SpawnId));
                var (ax, ay) = GetHoverEllipseAxes(m.SpawnId);
                rx = ax * scale;
                ry = ay * scale;
            }
            else if (m.Type == PlacedMissileType.Grand && m.GrandDirection != 0)
            {
                Vector2 w     = ScreenToWorld(vp, mousePos);
                float   halfD = (m.GrandDiameter > 0 ? m.GrandDiameter : 2) * 0.5f;
                Vector2 tc    = TileCenter(m.TilePos);
                bool    hit   = m.GrandDirection <= 2
                    ? Mathf.Abs(w.x - tc.x) <= halfD && w.y >= -GridRows * 0.5f && w.y <= GridRows * 0.5f
                    : Mathf.Abs(w.y - tc.y) <= halfD && w.x >= -GridCols * 0.5f && w.x <= GridCols * 0.5f;
                if (hit) result.Add(m.Id);
                continue;
            }
            else
            {
                center = WorldToScreen(vp, TileCenter(m.TilePos));
                float r = m.Type == PlacedMissileType.Grand
                    ? m.GrandDiameter / 2f * scale
                    : 0.45f * scale;
                rx = ry = r;
            }

            float dx = mousePos.x - center.x;
            float dy = mousePos.y - center.y;
            if (rx > 0f && ry > 0f && (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1f)
                result.Add(m.Id);
        }
        return result;
    }

    // ── 씬뷰 렌더링 ──────────────────────────────────────────────────────────

    // RenderTexture 크기를 뷰포트에 맞추고 카메라를 수동 렌더
    private void DrawSceneViewport(Rect vp)
    {
        if (!sceneInitialized) return;

        int w = Mathf.Max(1, (int)vp.width);
        int h = Mathf.Max(1, (int)vp.height);

        if (renderTexture == null || renderTexture.width != w || renderTexture.height != h)
        {
            if (renderTexture != null)
            {
                sceneCamera.targetTexture = null;
                renderTexture.Release();
                DestroyImmediate(renderTexture);
            }
            renderTexture             = new RenderTexture(w, h, 24);
            sceneCamera.targetTexture = renderTexture;
        }

        sceneCamera.Render();
        GUI.DrawTexture(vp, renderTexture, ScaleMode.StretchToFill, false);
        DrawSceneDirectionLabels(vp);
        DrawSceneHoverHighlight(vp);
        DrawSceneSelectedMarker(vp);
        DrawSceneSelectedSpawnMarkers(vp);
        DrawSceneMissileSelectedMarkers(vp);
        DrawOrientationGizmo(vp);
    }

    // 씬뷰 N/S/E/W 레이블 오버레이 — 실제 플랫폼 크기 기반으로 스폰 영역 바깥에 표시
    private void DrawSceneDirectionLabels(Rect vp)
    {
        if (!sceneInitialized || sceneCamera == null) return;

        float halfPlatX = GridCols * tileXSize * 0.5f;
        float halfPlatZ = GridRows * tileZSize * 0.5f;
        float labelPad  = Mathf.Max(tileXSize, tileZSize) * 1.5f;

        DrawSceneWorldLabel(vp, new Vector3(0f,                        0f,  halfPlatZ + SpawnOffset + labelPad), "N");
        DrawSceneWorldLabel(vp, new Vector3(0f,                        0f, -halfPlatZ - SpawnOffset - labelPad), "S");
        DrawSceneWorldLabel(vp, new Vector3( halfPlatX + SpawnOffset + labelPad, 0f, 0f),                        "E");
        DrawSceneWorldLabel(vp, new Vector3(-halfPlatX - SpawnOffset - labelPad, 0f, 0f),                        "W");
    }

    // 3D 월드 좌표 → 뷰포트 픽셀로 투영 후 레이블 그림
    private void DrawSceneWorldLabel(Rect vp, Vector3 worldPos, string text)
    {
        Vector3 vpPoint = sceneCamera.WorldToViewportPoint(worldPos);
        if (vpPoint.z <= 0f) return;

        float px        = vp.x + vpPoint.x        * vp.width;
        float py        = vp.y + (1f - vpPoint.y) * vp.height;
        Rect  labelRect = new Rect(px - 10f, py - 8f, 20f, 16f);
        if (vp.Overlaps(labelRect))
            GUI.Label(labelRect, text, EditorStyles.centeredGreyMiniLabel);
    }

    // 씬뷰 선택된 타일 마커 — selectedTiles 전체 순회하며 반투명 채우기 + 외곽선 렌더
    // tileWorldPositions에서 실제 타일 월드 위치를 가져와 사용 — 재계산 오차 방지
    private void DrawSceneSelectedMarker(Rect vp)
    {
        if (!sceneInitialized || sceneCamera == null || selectedTiles.Count == 0) return;

        float hw = tileXSize * 0.5f;
        float hd = tileZSize * 0.5f;

        foreach (Vector2Int t in selectedTiles)
        {
            if (!tileWorldPositions.TryGetValue(t, out Vector3 tileCenter)) continue;

            float cx = tileCenter.x;
            float cz = tileCenter.z;

            // 타일 4 코너 (y=0.02f — 타일 면 살짝 위)
            var corners = new Vector3[]
            {
                new Vector3(cx - hw, 0.02f, cz + hd),
                new Vector3(cx + hw, 0.02f, cz + hd),
                new Vector3(cx + hw, 0.02f, cz - hd),
                new Vector3(cx - hw, 0.02f, cz - hd),
            };

            // 카메라 앞쪽에 있는지 확인 (하나라도 z <= 0 이면 스킵)
            bool behindCamera = false;
            foreach (Vector3 c in corners)
                if (sceneCamera.WorldToViewportPoint(c).z <= 0f) { behindCamera = true; break; }
            if (behindCamera) continue;

            // 코너 → 화면 픽셀 변환
            var screenVerts = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 vp3 = sceneCamera.WorldToViewportPoint(corners[i]);
                screenVerts[i] = new Vector3(
                    vp.x + vp3.x * vp.width,
                    vp.y + (1f - vp3.y) * vp.height,
                    0f);
            }

            // 반투명 채우기 + 외곽선 (탑뷰 TileSelectedColor 오버레이와 동일한 UX)
            Handles.DrawSolidRectangleWithOutline(
                screenVerts,
                new Color(SceneSelectionColor.r, SceneSelectionColor.g, SceneSelectionColor.b, 0.20f),
                SceneSelectionColor);

            // 타일 좌표 레이블 (중심 위)
            Vector3 centerVP = sceneCamera.WorldToViewportPoint(new Vector3(cx, 0f, cz));
            float   px       = vp.x + centerVP.x * vp.width;
            float   py       = vp.y + (1f - centerVP.y) * vp.height;
            GUI.Label(new Rect(px - 20f, py - 16f, 40f, 14f),
                      $"({t.x},{t.y})", EditorStyles.centeredGreyMiniLabel);
        }
    }

    // 씬뷰 선택된 스폰포인트 외곽선 마커 — 큐브 중심을 화면에 투영해 사각형 윤곽 그림
    // selectedSpawnPoints는 탑뷰와 공유. 선택 상태가 씬뷰 ↔ 탑뷰 전환 시에도 유지됨.
    private void DrawSceneSelectedSpawnMarkers(Rect vp)
    {
        if (!sceneInitialized || sceneCamera == null || selectedSpawnPoints.Count == 0) return;

        const float markerHalf = 9f;  // 마커 반경 (픽셀)

        foreach (string spawnId in selectedSpawnPoints)
        {
            if (!spawnWorldPositions.TryGetValue(spawnId, out Vector3 worldPos)) continue;

            Vector3 vpPoint = sceneCamera.WorldToViewportPoint(worldPos);
            if (vpPoint.z <= 0f) continue;

            float px = vp.x + vpPoint.x        * vp.width;
            float py = vp.y + (1f - vpPoint.y) * vp.height;
            if (!vp.Contains(new Vector2(px, py))) continue;

            float x0 = px - markerHalf, x1 = px + markerHalf;
            float y0 = py - markerHalf, y1 = py + markerHalf;

            Handles.color = SpawnPointSelectedColor;
            Handles.DrawLine(new Vector3(x0, y0), new Vector3(x1, y0));
            Handles.DrawLine(new Vector3(x1, y0), new Vector3(x1, y1));
            Handles.DrawLine(new Vector3(x1, y1), new Vector3(x0, y1));
            Handles.DrawLine(new Vector3(x0, y1), new Vector3(x0, y0));
        }

        Handles.color = Color.white;
    }

    // 씬뷰 선택된 미사일 흰색 윤곽선 — selectedMissileIds를 탑뷰와 공유
    // 탑뷰에서 선택한 미사일이 씬뷰에도 동일하게 표시됨 (ADR-018)
    private void DrawSceneMissileSelectedMarkers(Rect vp)
    {
        if (!sceneInitialized || sceneCamera == null || selectedMissileIds.Count == 0) return;

        Color oldColor = Handles.color;
        Handles.color = Color.white;

        foreach (int id in selectedMissileIds)
        {
            int idx = -1;
            for (int i = 0; i < placedMissiles.Count; i++)
                if (placedMissiles[i].Id == id) { idx = i; break; }
            if (idx < 0) continue;

            var m = placedMissiles[idx];
            Vector3 worldPos;
            if (m.Type == PlacedMissileType.Hover)
            {
                if (!spawnWorldPositions.TryGetValue(m.SpawnId, out worldPos)) continue;
                worldPos.y += HoverSpawnYOffset;
            }
            else
            {
                if (!tileWorldPositions.TryGetValue(m.TilePos, out worldPos)) continue;
                worldPos.y = tileSurfaceY;
            }

            Vector3 vpPoint = sceneCamera.WorldToViewportPoint(worldPos);
            if (vpPoint.z <= 0f) continue;

            float px = vp.x + vpPoint.x        * vp.width;
            float py = vp.y + (1f - vpPoint.y) * vp.height;
            if (!vp.Contains(new Vector2(px, py))) continue;

            // 타입별 크기: Falling·Hover = 고정 반경, Grand = 직경 비례
            float r = m.Type == PlacedMissileType.Grand
                ? Mathf.Max(10f, m.GrandDiameter * 6f)
                : 12f;

            // DrawPolyLine으로 원 근사 (top view의 hover highlight와 동일한 방식)
            const int kSegs = 24;
            var circlePts = new Vector3[kSegs + 1];
            for (int s = 0; s <= kSegs; s++)
            {
                float a = s * (2f * Mathf.PI / kSegs);
                circlePts[s] = new Vector3(px + r * Mathf.Cos(a), py + r * Mathf.Sin(a), 0f);
            }
            Handles.DrawPolyLine(circlePts);
        }

        Handles.color = oldColor;
    }

    // 씬뷰 호버 하이라이트 — 마우스 아래 타일/스폰포인트를 노란 윤곽선으로 강조
    private void DrawSceneHoverHighlight(Rect vp)
    {
        if (!sceneInitialized || sceneCamera == null) return;

        Color oldColor = Handles.color;
        Handles.color  = MissileHoverHighlightColor;

        // 타일 호버 — 선택 마커와 동일한 방식, 채우기 없이 외곽선만
        if (sceneHoveredTile.HasValue &&
            tileWorldPositions.TryGetValue(sceneHoveredTile.Value, out Vector3 tileCenter))
        {
            float hw = tileXSize * 0.5f;
            float hd = tileZSize * 0.5f;
            float cx = tileCenter.x, cz = tileCenter.z;

            var corners = new Vector3[]
            {
                new Vector3(cx - hw, 0.02f, cz + hd),
                new Vector3(cx + hw, 0.02f, cz + hd),
                new Vector3(cx + hw, 0.02f, cz - hd),
                new Vector3(cx - hw, 0.02f, cz - hd),
            };

            bool behind = false;
            foreach (Vector3 c in corners)
                if (sceneCamera.WorldToViewportPoint(c).z <= 0f) { behind = true; break; }

            if (!behind)
            {
                var sv = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    Vector3 v = sceneCamera.WorldToViewportPoint(corners[i]);
                    sv[i] = new Vector3(vp.x + v.x * vp.width, vp.y + (1f - v.y) * vp.height, 0f);
                }
                Handles.DrawSolidRectangleWithOutline(sv, new Color(1f, 0.85f, 0.2f, 0.08f), MissileHoverHighlightColor);
            }
        }

        // 스폰포인트 호버 — 사각 윤곽선 (선택 마커보다 약간 큰 크기)
        if (sceneHoveredSpawnId != null &&
            spawnWorldPositions.TryGetValue(sceneHoveredSpawnId, out Vector3 spawnPos))
        {
            Vector3 vpPoint = sceneCamera.WorldToViewportPoint(spawnPos);
            if (vpPoint.z > 0f)
            {
                float px = vp.x + vpPoint.x        * vp.width;
                float py = vp.y + (1f - vpPoint.y) * vp.height;
                if (vp.Contains(new Vector2(px, py)))
                {
                    const float half = 12f;
                    Handles.DrawLine(new Vector3(px - half, py - half), new Vector3(px + half, py - half));
                    Handles.DrawLine(new Vector3(px + half, py - half), new Vector3(px + half, py + half));
                    Handles.DrawLine(new Vector3(px + half, py + half), new Vector3(px - half, py + half));
                    Handles.DrawLine(new Vector3(px - half, py + half), new Vector3(px - half, py - half));
                }
            }
        }

        Handles.color = oldColor;
    }

    // 스폰포인트 큐브 오브젝트 일괄 생성 — InitSceneView에서 한 번만 호출
    // MissileSpawner.CreateSpawnPoint() 를 에디터 씬에 그대로 재현.
    // platformOrigin = GetTile(0) 위치, tileXSize/tileZSize = 실제 메시 크기.
    // ── 씬뷰 미사일 ghost 관리 (ADR-016) ─────────────────────────────────────

    // placedMissiles와 missileGhosts를 완전 동기화.
    // 씬뷰 초기화 직후(InitSceneView) 호출 — 이미 배치된 미사일이 있을 때 ghost 일괄 생성.
    private void SyncAllMissileGhosts()
    {
        // stale ghost 제거
        var staleIds = new List<int>();
        foreach (var kv in missileGhosts)
        {
            bool found = false;
            for (int i = 0; i < placedMissiles.Count; i++)
                if (placedMissiles[i].Id == kv.Key) { found = true; break; }
            if (!found) staleIds.Add(kv.Key);
        }
        foreach (int id in staleIds) DestroyMissileGhost(id);

        // 누락된 ghost 생성
        for (int i = 0; i < placedMissiles.Count; i++)
            if (!missileGhosts.ContainsKey(placedMissiles[i].Id))
                SpawnMissileGhost(placedMissiles[i]);
    }

    // 미사일 1개에 대한 씬뷰 ghost 오브젝트 생성
    // 배치된 미사일의 씬뷰 ghost를 타입별 primitive로 생성 (ADR-016)
    // 실제 게임 프리팹 대신 primitive를 사용해 "배치됨" 상태를 명확하게 표현.
    //   Falling → 수직 캡슐 (낙하 위치 마커)
    //   Grand   → 납작한 디스크 (착탄 반경 시각화)
    //   Hover   → 수평 캡슐, spawnId 방향으로 회전 (진입 방향 표시)
    private void SpawnMissileGhost(PlacedMissile m)
    {
        if (!sceneInitialized) return;
        if (missileGhosts.ContainsKey(m.Id)) return;

        Vector3    pos;
        Quaternion rot = Quaternion.identity;

        if (m.Type == PlacedMissileType.Hover)
        {
            if (!spawnWorldPositions.TryGetValue(m.SpawnId, out pos)) return;
            // 스폰 위치 → 플랫폼 중심쪽 축 방향: 주축(X vs Z) 중 더 큰 쪽으로 스냅
            var toCenter = new Vector3(-pos.x, 0f, -pos.z);
            if (toCenter.sqrMagnitude < 0.001f) toCenter = Vector3.forward;
            else if (Mathf.Abs(toCenter.x) >= Mathf.Abs(toCenter.z))
                toCenter = new Vector3(Mathf.Sign(toCenter.x), 0f, 0f);
            else
                toCenter = new Vector3(0f, 0f, Mathf.Sign(toCenter.z));
            rot = Quaternion.LookRotation(toCenter, Vector3.up);
            pos.y += HoverSpawnYOffset;   // 게임 스폰 Y 오프셋 (MissileSpawner: +2.5f)
        }
        else
        {
            if (!tileWorldPositions.TryGetValue(m.TilePos, out pos)) return;
        }

        GameObject go;
        if (m.Type == PlacedMissileType.Hover)
        {
            // Hover: 실제 프리팹 인스턴스 — 배치 상태를 게임 오브젝트 그대로 표현
            var prefab = (m.PrefabIndex >= 0 && m.PrefabIndex < palettePrefabs.Count)
                ? palettePrefabs[m.PrefabIndex] : null;
            if (prefab == null) return;
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, editorScene);
            go.transform.SetPositionAndRotation(pos, rot);
            // 모든 Collider 비활성화 — Physics.Raycast 오염 방지
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
        }
        else if (m.Type == PlacedMissileType.Falling)
        {
            var mat = CreateGhostMaterial(PlacedMissileType.Falling, RQOffsetFalling);
            missileMaterials.Add(mat);

            // 부모 빈 GO — disc + 스폰 캡슐을 묶어 단일 Id로 관리
            go = new GameObject("FallingGhost_" + m.Id);
            SceneManager.MoveGameObjectToScene(go, editorScene);

            // 타일 위 경고 원형 디스크 — 프로시저럴 양면 메시 (컬링 문제 없음)
            // scale.x = tileXSize, scale.z = tileZSize → 메시 반지름 0.5 기준으로 타일 크기 매핑
            CreateFlatMeshObject("FallingDisc", new Vector3(pos.x, tileSurfaceY + 0.15f, pos.z),
                new Vector3(tileXSize, 1f, tileZSize), mat, go.transform, discMesh);

            // 스폰 높이 캡슐 (GlobalData.MissileDropPoint = 50)
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(go.transform);
            capsule.transform.position   = new Vector3(pos.x, FallingSpawnHeight + 0.5f, pos.z);
            capsule.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
            capsule.GetComponent<Collider>().enabled        = false;
            capsule.GetComponent<Renderer>().sharedMaterial = mat;
        }
        else  // Grand — 부모 빈 GO: 프리팹(스폰/엣지 위치) + 위험범위 디스크/스트립(타일 표면)
        {
            var prefab = (m.PrefabIndex >= 0 && m.PrefabIndex < palettePrefabs.Count)
                ? palettePrefabs[m.PrefabIndex] : null;
            if (prefab == null) return;

            go = new GameObject("GrandGhost_" + m.Id);
            SceneManager.MoveGameObjectToScene(go, editorScene);

            // 방향별 프리팹 위치·회전 계산
            Vector3    prefabPos;
            Quaternion prefabRot;

            if (m.GrandDirection == 0)
            {
                // Vertical: 스폰 높이에서 아래 방향
                prefabPos = new Vector3(pos.x, FallingSpawnHeight, pos.z);
                prefabRot = Quaternion.Euler(180f, 0f, 0f);
            }
            else
            {
                // Horizontal: 해당 방향 플랫폼 엣지에서 수평 진입
                float midY = tileSurfaceY + 1f;
                switch (m.GrandDirection)
                {
                    case 1:  // N→S: 북쪽 엣지에서 남으로 (SpawnOffset 거리)
                        tileWorldPositions.TryGetValue(new Vector2Int(m.TilePos.x, 0), out var nTile);
                        prefabPos = new Vector3(nTile.x, midY, nTile.z + SpawnOffset);
                        prefabRot = Quaternion.Euler(-90f, 0f, 0f);
                        break;
                    case 2:  // S→N: 남쪽 엣지에서 북으로
                        tileWorldPositions.TryGetValue(new Vector2Int(m.TilePos.x, GridRows - 1), out var sTile);
                        prefabPos = new Vector3(sTile.x, midY, sTile.z - SpawnOffset);
                        prefabRot = Quaternion.Euler(90f, 0f, 0f);
                        break;
                    case 3:  // E→W: 동쪽 엣지에서 서로
                        tileWorldPositions.TryGetValue(new Vector2Int(GridCols - 1, m.TilePos.y), out var eTile);
                        prefabPos = new Vector3(eTile.x + SpawnOffset, midY, eTile.z);
                        prefabRot = Quaternion.Euler(0f, 0f, 90f);
                        break;
                    default:  // case 4: W→E: 서쪽 엣지에서 동으로
                        tileWorldPositions.TryGetValue(new Vector2Int(0, m.TilePos.y), out var wTile);
                        prefabPos = new Vector3(wTile.x - SpawnOffset, midY, wTile.z);
                        prefabRot = Quaternion.Euler(0f, 0f, -90f);
                        break;
                }
            }

            var prefabInst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, editorScene);
            prefabInst.transform.SetParent(go.transform);
            prefabInst.transform.SetPositionAndRotation(prefabPos, prefabRot);
            var cols = prefabInst.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

            // 프리팹 모델 스케일 — GrandMissile.Initialize() 로직 재현 (tileXSize * diameter / meshSize.x)
            int diam = m.GrandDiameter > 0 ? m.GrandDiameter : 2;
            var mf = prefabInst.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var mt      = mf.transform;
                var baseScl = mt.localScale;
                var meshSz  = Vector3.Scale(mf.sharedMesh.bounds.size, mt.lossyScale);
                float desired = tileXSize * diam;
                float sf      = meshSz.x > 0.001f ? desired / meshSz.x : 1f;
                mt.localScale = new Vector3(baseScl.x * sf, baseScl.y * sf * (2f / 3f), baseScl.z * sf);
            }

            // 타일 표면 위험 범위 디스크 — 프로시저럴 양면 메시 (컬링 문제 없음)
            // Grand H(queue+0, Y+0.03) → Grand V(queue+1, Y+0.10) → Falling(queue+3, Y+0.15)
            float worldD  = diam * tileXSize;
            float worldDZ = diam * tileZSize;

            if (m.GrandDirection == 0)
            {
                // Vertical: 원형 디스크 — Grand H 위, Falling 아래
                var mat = CreateGhostMaterial(PlacedMissileType.Grand, RQOffsetGrandV);
                missileMaterials.Add(mat);
                CreateFlatMeshObject("GrandVDisc", new Vector3(pos.x, tileSurfaceY + 0.10f, pos.z),
                    new Vector3(worldD, 1f, worldDZ), mat, go.transform, discMesh);
            }
            else
            {
                // Horizontal: 사각형 스트립 — 최배경
                var mat = CreateGhostMaterial(PlacedMissileType.Grand, RQOffsetGrandH);
                var ghColor = new Color(MissileGrandHColor.r, MissileGrandHColor.g, MissileGrandHColor.b, 0.55f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ghColor);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", ghColor);
                missileMaterials.Add(mat);

                if (m.GrandDirection == 1 || m.GrandDirection == 2)
                {
                    // N↔S: 열 방향 — 플랫폼 시각 중심(tileMeshCenterOffset.y) 기준
                    float stripLengthZ = GridRows * tileZSize;
                    tileWorldPositions.TryGetValue(m.TilePos, out var colTile);
                    CreateFlatMeshObject("GrandHStrip",
                        new Vector3(colTile.x, tileSurfaceY + 0.03f, tileMeshCenterOffset.y),
                        new Vector3(worldD, 1f, stripLengthZ), mat, go.transform, quadMesh);
                }
                else
                {
                    // E↔W: 행 방향 — 플랫폼 시각 중심(tileMeshCenterOffset.x) 기준
                    float stripLengthX = GridCols * tileXSize;
                    tileWorldPositions.TryGetValue(m.TilePos, out var rowTile);
                    CreateFlatMeshObject("GrandHStrip",
                        new Vector3(tileMeshCenterOffset.x, tileSurfaceY + 0.03f, rowTile.z),
                        new Vector3(stripLengthX, 1f, worldDZ), mat, go.transform, quadMesh);
                }
            }
        }

        missileGhosts[m.Id] = go;
    }

    // ghost 오브젝트 파괴 및 딕셔너리에서 제거
    private void DestroyMissileGhost(int missileId)
    {
        if (!missileGhosts.TryGetValue(missileId, out var go)) return;
        if (go != null) Object.DestroyImmediate(go);
        missileGhosts.Remove(missileId);
    }

    // 양면 플랫 디스크 메시 생성 — 법선 ±Y 양방향, 컬링 문제 없음
    private static Mesh CreateDoubleSidedDiscMesh(int segments = 32)
    {
        // 꼭짓점: 중심(0) + 둘레(segments) × 2셋 (위/아래 법선)
        int vertCount = (1 + segments) * 2;
        var verts   = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var tris    = new int[segments * 3 * 2]; // 양면

        // 윗면 — 중심 인덱스 0, 둘레 1~segments
        verts[0]   = Vector3.zero;
        normals[0] = Vector3.up;
        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            verts[1 + i]   = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
            normals[1 + i] = Vector3.up;
        }
        for (int i = 0; i < segments; i++)
        {
            int t = i * 3;
            tris[t]     = 0;
            tris[t + 1] = 1 + i;
            tris[t + 2] = 1 + (i + 1) % segments;
        }

        // 아랫면 — 중심 인덱스 offset, 삼각형 와인딩 반전
        int off = 1 + segments;
        verts[off]   = Vector3.zero;
        normals[off] = Vector3.down;
        for (int i = 0; i < segments; i++)
        {
            verts[off + 1 + i]   = verts[1 + i];
            normals[off + 1 + i] = Vector3.down;
        }
        int triOff = segments * 3;
        for (int i = 0; i < segments; i++)
        {
            int t = triOff + i * 3;
            tris[t]     = off;
            tris[t + 1] = off + 1 + (i + 1) % segments;
            tris[t + 2] = off + 1 + i;
        }

        var mesh = new Mesh { name = "DoubleSidedDisc" };
        mesh.vertices  = verts;
        mesh.normals   = normals;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    // 양면 플랫 사각형 메시 생성 — XZ 평면, 크기 1×1 (중심 원점), 법선 ±Y
    private static Mesh CreateDoubleSidedQuadMesh()
    {
        var verts = new Vector3[]
        {
            // 윗면 (법선 +Y)
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f),
            // 아랫면 (법선 -Y) — 같은 꼭짓점, 와인딩 반전
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f),
        };
        var normals = new Vector3[]
        {
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,
        };
        var tris = new int[]
        {
            0, 2, 1,  0, 3, 2,   // 윗면
            4, 5, 6,  4, 6, 7,   // 아랫면 (와인딩 반전)
        };

        var mesh = new Mesh { name = "DoubleSidedQuad" };
        mesh.vertices  = verts;
        mesh.normals   = normals;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    // 양면 플랫 메시 GameObject 생성 — mesh 파라미터로 원형/사각형 선택
    private GameObject CreateFlatMeshObject(string name, Vector3 position, Vector3 scale, Material mat, Transform parent, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position   = position;
        go.transform.localScale = scale;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        return go;
    }

    // 씬뷰 3D ghost 렌더링 순서 — render queue 오프셋
    // 숫자가 클수록 나중에(위에) 그려짐
    private const int RQOffsetGrandH  = 0;  // 최배경
    private const int RQOffsetGrandV  = 1;
    private const int RQOffsetHover   = 2;
    private const int RQOffsetFalling = 3;  // 최전경

    // 타입별 반투명 Unlit 머티리얼 생성 (URP / Built-in 양쪽 지원)
    // renderQueueOffset: 같은 Transparent 큐 안에서 층별 순서 강제
    private Material CreateGhostMaterial(PlacedMissileType type, int renderQueueOffset = 0)
    {
        Color baseColor;
        if (type == PlacedMissileType.Grand)
            baseColor = new Color(MissileGrandColor.r,   MissileGrandColor.g,   MissileGrandColor.b,   0.55f);
        else if (type == PlacedMissileType.Hover)
            baseColor = new Color(MissileHoverColor.r,   MissileHoverColor.g,   MissileHoverColor.b,   0.55f);
        else
            baseColor = new Color(MissileFallingColor.r, MissileFallingColor.g, MissileFallingColor.b, 0.55f);

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);

        int baseQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + renderQueueOffset;

        if (mat.HasProperty("_Surface"))
        {
            // URP: Surface Type = Transparent
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend",   0f);
            mat.SetFloat("_ZWrite",  0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = baseQueue;
        }
        else
        {
            // Built-in Standard: Transparent mode
            mat.SetFloat("_Mode",   3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",   0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = baseQueue;
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color",    baseColor);

        return mat;
    }

    private void CreateSpawnPointObjects()
    {
        Material cardinalMat = CreateSpawnMaterial(SpawnPointCardinalColor);
        Material diagonalMat = CreateSpawnMaterial(SpawnPointDiagonalColor);

        // tile(row, col) 중심 좌표 헬퍼
        Vector3 TilePos(int row, int col) => new Vector3(
            platformOrigin.x + col * tileXSize,
            0f,
            platformOrigin.z - row * tileZSize);

        // ── Cardinal ─────────────────────────────────────────────────────────

        // North: GetTile(0), z+10, x-=tileXScale/2
        Vector3 northBase = TilePos(0, 0);
        northBase.z += SpawnOffset;
        northBase.x -= tileXSize * 0.5f;
        for (int i = 0; i < GridCols; i++)
            SpawnCube(new Vector3(northBase.x + tileXSize * i, 0.25f, northBase.z), cardinalMat, $"N:{i}");

        // South: GetTile(90)=tile(9,0), z-10, x-=tileXScale/2
        Vector3 southBase = TilePos(GridRows - 1, 0);
        southBase.z -= SpawnOffset;
        southBase.x -= tileXSize * 0.5f;
        for (int i = 0; i < GridCols; i++)
            SpawnCube(new Vector3(southBase.x + tileXSize * i, 0.25f, southBase.z), cardinalMat, $"S:{i}");

        // East: GetTile(9)=tile(0,9), x+10, z+=tileZScale/2
        Vector3 eastBase = TilePos(0, GridCols - 1);
        eastBase.x += SpawnOffset;
        eastBase.z += tileZSize * 0.5f;
        for (int i = 0; i < GridRows; i++)
            SpawnCube(new Vector3(eastBase.x, 0.25f, eastBase.z - tileZSize * i), cardinalMat, $"E:{i}");

        // West: GetTile(0), x-10, z+=tileZScale/2
        Vector3 westBase = TilePos(0, 0);
        westBase.x -= SpawnOffset;
        westBase.z += tileZSize * 0.5f;
        for (int i = 0; i < GridRows; i++)
            SpawnCube(new Vector3(westBase.x, 0.25f, westBase.z - tileZSize * i), cardinalMat, $"W:{i}");

        // ── Diagonal ─────────────────────────────────────────────────────────
        // 각 모서리 타일 중심에서 (x-=tileX/2, z+=tileZ/2) 로 코너 보정 후 ±SpawnOffset

        // NE: GetTile(9)=tile(0,9)
        Vector3 ne = TilePos(0, GridCols - 1);
        ne.x = ne.x - tileXSize * 0.5f + SpawnOffset;
        ne.z = ne.z + tileZSize * 0.5f + SpawnOffset;
        SpawnCube(new Vector3(ne.x, 0.25f, ne.z), diagonalMat, "NE");

        // NW: GetTile(0)=tile(0,0)
        Vector3 nw = TilePos(0, 0);
        nw.x = nw.x - tileXSize * 0.5f - SpawnOffset;
        nw.z = nw.z + tileZSize * 0.5f + SpawnOffset;
        SpawnCube(new Vector3(nw.x, 0.25f, nw.z), diagonalMat, "NW");

        // SE: GetTile(99)=tile(9,9)
        Vector3 se = TilePos(GridRows - 1, GridCols - 1);
        se.x = se.x - tileXSize * 0.5f + SpawnOffset;
        se.z = se.z + tileZSize * 0.5f - SpawnOffset;
        SpawnCube(new Vector3(se.x, 0.25f, se.z), diagonalMat, "SE");

        // SW: GetTile(90)=tile(9,0)
        Vector3 sw = TilePos(GridRows - 1, 0);
        sw.x = sw.x - tileXSize * 0.5f - SpawnOffset;
        sw.z = sw.z + tileZSize * 0.5f - SpawnOffset;
        SpawnCube(new Vector3(sw.x, 0.25f, sw.z), diagonalMat, "SW");
    }

    // spawnId: "N:3", "E:0" (cardinal) / "NE", "SW" (diagonal) — 씬뷰 클릭 감지용 룩업에 등록
    // 콜라이더는 Physics.Raycast 선택 감지에 필요하므로 유지
    private void SpawnCube(Vector3 pos, Material mat, string spawnId)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 0.5f;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        spawnByInstanceId[go.GetInstanceID()]  = spawnId;
        spawnWorldPositions[spawnId]           = pos;
        SceneManager.MoveGameObjectToScene(go, editorScene);
    }

    // URP / 빌트인 렌더 파이프라인 양쪽에서 동작하는 Unlit 컬러 머티리얼 생성
    private Material CreateSpawnMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat    = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.color = color;
        spawnMaterials.Add(mat);
        return mat;
    }

    // 씬뷰 우측 하단 — XYZ 방향 기즈모 (→ ADR-003)
    // Handles.DrawLine 으로 GUI 픽셀 좌표계에 직접 그림.
    // 카메라 right/up 기준으로 월드 축을 2D 투영, 뒤쪽 축을 먼저 그려 depth-sort 효과.
    private void DrawOrientationGizmo(Rect vp)
    {
        if (!sceneInitialized || sceneCamera == null) return;

        const float gizmoHalf  = 30f;   // 배경 정사각형 반경 (픽셀)
        const float axisRadius = 22f;   // 축 선 길이 (픽셀)
        const float dotSize    =  5f;   // 끝점 dot 크기 (픽셀)
        const float margin     =  8f;

        var center = new Vector2(
            vp.x + vp.width  - gizmoHalf - margin,
            vp.y + vp.height - gizmoHalf - margin);

        // 반투명 배경
        EditorGUI.DrawRect(
            new Rect(center.x - gizmoHalf, center.y - gizmoHalf, gizmoHalf * 2f, gizmoHalf * 2f),
            new Color(0f, 0f, 0f, 0.35f));

        Vector3 camRight = sceneCamera.transform.right;
        Vector3 camUp    = sceneCamera.transform.up;

        // 월드 축 방향 → 화면 2D 오프셋 (카메라 right/up 기준 투영)
        Vector2 Project(Vector3 worldAxis) => new Vector2(
             Vector3.Dot(worldAxis, camRight) * axisRadius,
            -Vector3.Dot(worldAxis, camUp)    * axisRadius);

        var axes = new (Vector3 dir, Color color, string label)[]
        {
            (Vector3.right,   new Color(0.95f, 0.25f, 0.25f), "X"),
            (Vector3.up,      new Color(0.25f, 0.90f, 0.25f), "Y"),
            (Vector3.forward, new Color(0.25f, 0.55f, 1.00f), "Z"),
        };

        // 뒤쪽 축 먼저 그려 앞쪽 축이 위로 오게 depth-sort
        System.Array.Sort(axes, (a, b) =>
            Vector3.Dot(b.dir, sceneCamera.transform.forward)
                .CompareTo(Vector3.Dot(a.dir, sceneCamera.transform.forward)));

        var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

        foreach (var (dir, color, label) in axes)
        {
            Vector2 end = center + Project(dir);

            // 축 선
            Handles.color = color;
            Handles.DrawLine(new Vector3(center.x, center.y), new Vector3(end.x, end.y));

            // 끝점 dot
            EditorGUI.DrawRect(
                new Rect(end.x - dotSize * 0.5f, end.y - dotSize * 0.5f, dotSize, dotSize), color);

            // 레이블
            var labelRect = new Rect(end.x - 8f, end.y - 8f, 16f, 16f);
            if (vp.Overlaps(labelRect))
            {
                labelStyle.normal.textColor = color;
                GUI.Label(labelRect, label, labelStyle);
            }
        }

        Handles.color = Color.white;  // restore
    }

    // Brush Tool 플로팅 배지 — 뷰포트 좌상단, Brush 활성 중에만 표시 (ADR-017 개정 1, ADR-018 개정 3)
    // Repaint: 배지 렌더, MouseDown: 클릭 해제 감지. 비활성 시 오버레이 없음.
    private void DrawInteractionModeOverlay(Rect vp)
    {
        if (!inPlaceMode) return;  // Brush 비활성: 오버레이 없음

        Event e = Event.current;
        if (e.type != EventType.Repaint && e.type != EventType.MouseDown) return;

        const float BtnSize = 32f;
        const float Pad     =  4f;
        const float Margin  =  8f;

        float panelW = BtnSize * 2 + Pad * 3;
        float panelH = BtnSize + Pad * 2;
        var panel = new Rect(vp.x + Margin, vp.y + Margin, panelW, panelH);

        Color brushColor = placingType switch
        {
            PlacedMissileType.Falling => MissileFallingColor,
            PlacedMissileType.Grand   => MissileGrandColor,
            PlacedMissileType.Hover   => MissileHoverColor,
            _                         => Color.white
        };

        var br = new Rect(panel.x + Pad, panel.y + Pad, BtnSize * 2 + Pad, BtnSize);

        if (e.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(panel, new Color(0.12f, 0.12f, 0.12f, 0.80f));
            EditorGUI.DrawRect(br, new Color(brushColor.r, brushColor.g, brushColor.b, 0.80f));
            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = Color.white } };
            GUI.Label(br, new GUIContent("Brush", $"Brush 모드 ({placingType}) — 우클릭 또는 Esc로 종료"), labelStyle);
        }
        if (e.type == EventType.MouseDown && e.button == 0 && br.Contains(e.mousePosition))
        {
            inPlaceMode          = false;
            selectedPaletteIndex = -1;
            e.Use(); Repaint();
        }
    }
    #endregion

    #region Playbar
    // Phase 2: 재생 바 — 상단 컨트롤 행 + 하단 타임라인 트랙
    //   컨트롤 행: 시간 표시 | 총 길이 편집 | +구간 버튼 | 종료 정책 토글
    //   타임라인 트랙: 클릭/드래그 → 시간 스크러빙, 구간 표시, 재생헤드
    private void DrawPlaybar()
    {
        float w  = position.width;
        float py = position.height - PlaybarHeight;
        EditorGUI.DrawRect(new Rect(0, py, w, PlaybarHeight), PlaybarBackground);

        // ── 컨트롤 행 (상단 26px) ────────────────────────────────────────────
        GUILayout.BeginArea(new Rect(4, py + 4, w - 8, 22));
        GUILayout.BeginHorizontal();

        // 현재 시간 표시
        GUILayout.Label($"{currentTime:F2} / {totalDuration:F2} s", EditorStyles.boldLabel,
                        GUILayout.Width(116));
        GUILayout.Space(6);

        // 총 재생 길이 편집 (0.1초 ~ 600초)
        GUILayout.Label("길이:", GUILayout.Width(24));
        float newDur = EditorGUILayout.DelayedFloatField(totalDuration, GUILayout.Width(42));
        if (newDur != totalDuration)
        {
            totalDuration = Mathf.Clamp(newDur, 0.1f, 600f);
            currentTime   = Mathf.Clamp(currentTime, 0f, totalDuration);
            // 구간 끝점도 클램프
            for (int i = 0; i < globalSegments.Count; i++)
            {
                var s = globalSegments[i];
                s.End = Mathf.Clamp(s.End, s.Start, totalDuration);
                globalSegments[i] = s;
            }
            ClampTimelineView();
        }
        GUILayout.Label("s", GUILayout.Width(10));
        GUILayout.Space(10);

        // 구간 추가 버튼
        if (GUILayout.Button("+ 구간", GUILayout.Height(20), GUILayout.Width(50)))
            AddGlobalSegment();

        GUILayout.FlexibleSpace();

        // 루프 토글 — 끝 도달 시 처음부터 재개
        GUI.backgroundColor = loopPlayback ? new Color(0.4f, 0.8f, 0.4f) : Color.white;
        if (GUILayout.Button("루프", GUILayout.Height(20), GUILayout.Width(34)))
            loopPlayback = !loopPlayback;
        GUI.backgroundColor = Color.white;
        GUILayout.Space(4);

        // 종료 정책 토글 — 재생 바 종료 시 스폰 오브젝트 처리 방식 (Phase 3에서 실제 동작)
        string policyLabel = endPolicy == EndOfPatternPolicy.Destroy ? "파괴" : "유지";
        if (GUILayout.Button(policyLabel, GUILayout.Height(20), GUILayout.Width(34)))
            endPolicy = endPolicy == EndOfPatternPolicy.Destroy
                ? EndOfPatternPolicy.KeepLast : EndOfPatternPolicy.Destroy;

        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        // ── 타임라인 트랙 (하단 28px) ──────────────────────────────────────────
        Rect trackRect = new Rect(4, py + 30, w - 8, 26);
        if (Event.current.type == EventType.Repaint)
            DrawTimelineTrack(trackRect);
        HandleTimelineInput(trackRect);
    }

    // 시간 → 트랙 x좌표 변환 (줌 적용)
    private float TimeToTrackX(Rect track, float t)
    {
        float visibleDuration = totalDuration / timelineZoom;
        return track.x + (t - timelineViewStart) / visibleDuration * track.width;
    }

    // 트랙 x좌표 → 시간 변환 (줌 적용)
    private float TrackXToTime(Rect track, float x)
    {
        float visibleDuration = totalDuration / timelineZoom;
        return timelineViewStart + (x - track.x) / track.width * visibleDuration;
    }

    // timelineViewStart를 유효 범위로 클램프
    private void ClampTimelineView()
    {
        float visibleDuration = totalDuration / timelineZoom;
        timelineViewStart = Mathf.Clamp(timelineViewStart, 0f, Mathf.Max(0f, totalDuration - visibleDuration));
    }

    // 타임라인 트랙 렌더링 — 구간 사각형 + 재생헤드 + 시간 눈금
    private void DrawTimelineTrack(Rect track)
    {
        // 배경
        EditorGUI.DrawRect(track, new Color(0.06f, 0.06f, 0.06f));

        if (totalDuration <= 0f) return;

        float visibleDuration = totalDuration / timelineZoom;
        float viewEnd         = timelineViewStart + visibleDuration;

        // 전체 구간들 — 선택 구간은 밝게 표시
        for (int i = 0; i < globalSegments.Count; i++)
        {
            var   seg = globalSegments[i];
            float x1  = TimeToTrackX(track, seg.Start);
            float x2  = TimeToTrackX(track, seg.End);
            if (x2 < track.x || x1 > track.xMax) continue;
            Color col = (i == selectedSegmentIndex)
                ? new Color(0.35f, 0.85f, 0.35f, 0.85f)
                : new Color(0.20f, 0.60f, 0.20f, 0.60f);
            float rx = Mathf.Max(x1, track.x);
            float rw = Mathf.Max(2f, Mathf.Min(x2, track.xMax) - rx);
            EditorGUI.DrawRect(new Rect(rx, track.y + 3, rw, track.height - 6), col);
        }

        // 시간 눈금 레이블 — visibleDuration 기준 간격 선택, interval < 1s 이면 소수점 표시
        float interval  = GetTimeMarkInterval(visibleDuration, track.width);
        float tickStart = Mathf.Ceil(timelineViewStart / interval) * interval;
        for (float t = tickStart; t <= viewEnd + 0.001f; t += interval)
        {
            float x = TimeToTrackX(track, t);
            if (x < track.x || x > track.xMax) continue;
            EditorGUI.DrawRect(new Rect(x, track.y, 1f, 4f), new Color(0.45f, 0.45f, 0.45f));
            string label = interval < 1f ? $"{t:F1}s" : $"{t:F0}s";
            GUI.Label(new Rect(x - 14f, track.y + 4f, 30f, 12f),
                      label, EditorStyles.centeredGreyMiniLabel);
        }

        // 재생헤드 — 흰색 세로선
        float px = TimeToTrackX(track, currentTime);
        if (px >= track.x && px <= track.xMax)
            EditorGUI.DrawRect(new Rect(px - 1f, track.y, 2f, track.height), Color.white);
    }

    // 눈금 간격 자동 계산 — 가시 범위 기준, 트랙 너비당 최소 40px 간격 유지 (최소 0.1s)
    private static float GetTimeMarkInterval(float visibleDuration, float trackWidth)
    {
        float[] options = { 0.1f, 0.2f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f };
        foreach (float iv in options)
        {
            if (visibleDuration / iv <= 0f) continue;
            if (trackWidth / (visibleDuration / iv) >= 40f) return iv;
        }
        return 60f;
    }

    // 타임라인 트랙 입력 — 좌클릭/드래그 스크러빙, 휠 줌, 미들 마우스 팬
    private void HandleTimelineInput(Rect trackRect)
    {
        Event e = Event.current;

        // 휠 줌 — 커서 아래 시간 고정
        if (e.type == EventType.ScrollWheel && trackRect.Contains(e.mousePosition))
        {
            float cursorTime      = TrackXToTime(trackRect, e.mousePosition.x);
            float zoomFactor      = e.delta.y > 0 ? 0.85f : 1f / 0.85f;
            float maxZoom         = Mathf.Max(1f, totalDuration / 0.1f);
            timelineZoom          = Mathf.Clamp(timelineZoom * zoomFactor, 1f, maxZoom);
            float visibleDuration = totalDuration / timelineZoom;
            float cursorRatio     = (e.mousePosition.x - trackRect.x) / trackRect.width;
            timelineViewStart     = cursorTime - cursorRatio * visibleDuration;
            ClampTimelineView();
            e.Use();
            Repaint();
        }

        // 미들 마우스 드래그 — 횡 팬
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) &&
            e.button == 2 && trackRect.Contains(e.mousePosition))
        {
            float visibleDuration  = totalDuration / timelineZoom;
            float secondsPerPixel  = visibleDuration / trackRect.width;
            timelineViewStart     -= e.delta.x * secondsPerPixel;
            ClampTimelineView();
            e.Use();
            Repaint();
        }

        // 좌클릭/드래그 — 스크러빙 + 구간 선택
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) &&
            e.button == 0 && trackRect.Contains(e.mousePosition))
        {
            float t = TrackXToTime(trackRect, e.mousePosition.x);
            currentTime = Mathf.Clamp(t, 0f, totalDuration);
            isPlaying   = false;

            // 클릭 시 구간 선택 — 가장 짧은 구간 우선
            if (e.type == EventType.MouseDown)
            {
                int   best     = -1;
                float bestSpan = float.MaxValue;
                for (int i = 0; i < globalSegments.Count; i++)
                {
                    var seg = globalSegments[i];
                    if (t >= seg.Start && t <= seg.End)
                    {
                        float span = seg.End - seg.Start;
                        if (span < bestSpan) { best = i; bestSpan = span; }
                    }
                }
                selectedSegmentIndex = best;
            }

            e.Use();
            Repaint();
        }

        // Delete → 선택 구간 삭제
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete &&
            selectedSegmentIndex >= 0 && selectedSegmentIndex < globalSegments.Count)
        {
            globalSegments.RemoveAt(selectedSegmentIndex);
            selectedSegmentIndex = -1;
            e.Use();
            Repaint();
        }
    }
    #endregion

    #region Input
    // 키보드 단축키 — OnGUI 최상단에서 호출해 컨트롤(FloatField 등)보다 먼저 이벤트를 가로챔
    private void HandleKeyboardShortcuts()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        // CustomSpeedField에 포커스가 있을 때 숫자·'.'·제어키 이외의 문자를 차단한다.
        // customSpeedFieldFocused는 DrawToolbar 렌더 이후 기록된 값(1프레임 지연)을 사용.
        if (customSpeedFieldFocused)
        {
            char c = e.character;
            // [주의] 초기 구현에서 c == 0 조건을 사용했으나 Backspace가 차단되는 버그가 있었다.
            // 원인: Unity에서 Backspace 키의 e.character는 null('\0', code=0)이 아닌
            //       Backspace 문자('\b', code=8)로 들어온다. 따라서 c == 0은 false가 된다.
            // 수정: c < 32 (ASCII 제어문자 0~31 전체)로 변경.
            //       Backspace=8, Enter=13, 방향키=0 등 비인쇄 제어문자를 모두 허용한다.
            //       Delete(code=127)는 제어문자 범위를 벗어나므로 별도로 c == 127 추가.
            bool allowed = c < 32 || c == 127 || char.IsDigit(c) || c == '.';
            if (!allowed) { e.Use(); return; }
        }

        switch (e.keyCode)
        {
            case KeyCode.Escape:
                if (inPlaceMode)
                {
                    // Brush 모드 종료 (ADR-017 개정 1)
                    inPlaceMode          = false;
                    selectedPaletteIndex = -1;
                    e.Use(); Repaint(); return;
                }
                // 미사일 선택 해제 우선, 그 다음 타일 선택 해제
                if (selectedMissileIds.Count > 0)
                {
                    selectedMissileIds.Clear();
                    overlapListIds.Clear();
                    e.Use(); Repaint(); return;
                }
                if (selectedTiles.Count > 0 || selectedSpawnPoints.Count > 0) PushUndo();
                selectedTiles.Clear();
                selectedSpawnPoints.Clear();
                CancelDrag();
                e.Use(); Repaint(); return;
            case KeyCode.Delete:
                // 선택 구간 있으면 타임라인에서 처리하도록 통과, 없으면 미사일·타일 삭제 (ADR-020)
                if (selectedSegmentIndex >= 0) break;
                DeleteSelectedObjects();
                e.Use(); Repaint(); return;
            case KeyCode.Z when e.control:
                UndoSelection(); e.Use(); return;
            case KeyCode.Y when e.control:
                RedoSelection(); e.Use(); return;
            case KeyCode.Space:
                GUIUtility.keyboardControl = 0;   // 텍스트 필드 포커스 해제 후 토글
                TogglePlay(); e.Use(); return;
        }
    }

    private void HandleInput()
    {
        Event e  = Event.current;
        Rect  vp = ViewportRect;

        // 드래그 박스: 진행 중이면 뷰포트 밖에서도 추적·완료 — 드래그 중 마우스가 창 밖으로 나가도 묶이지 않음
        if (dragStartPos != null)
        {
            if (e.type == EventType.MouseDrag && e.button == 0 && !e.alt)
            {
                dragEndPos = e.mousePosition;
                if (!isDragging && Vector2.Distance(dragStartPos.Value, dragEndPos) > 5f)
                    isDragging = true;
                if (isDragging) { e.Use(); Repaint(); }
                return;
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (isDragging)
                {
                    PushUndo();
                    if (currentMode == ViewMode.SceneView)
                        ApplyBoxSelectionSceneView(vp, dragStartPos.Value, dragEndPos, dragWasCtrl);
                    else
                        ApplyBoxSelectionTopDown(vp, dragStartPos.Value, dragEndPos, dragWasCtrl);
                }
                else if (vp.Contains(dragStartPos.Value))
                {
                    // 5px 미만 이동 → 클릭으로 처리. 시작 위치 기준.
                    PushUndo();
                    if (currentMode == ViewMode.SceneView)
                        ApplySingleClickSceneView(vp, dragStartPos.Value, dragWasCtrl);
                    else
                        ApplySingleClickTopDown(vp, dragStartPos.Value, dragWasCtrl);
                }
                CancelDrag();
                e.Use();
                Repaint();
                return;
            }
        }

        if (!vp.Contains(e.mousePosition)) return;

        if (currentMode == ViewMode.SceneView)
        {
            HandleSceneViewInput(e);
            return;
        }

        // ── 탑뷰 전용 입력 ──────────────────────────────────────────────────────

        // 마우스 이동 → 호버 타일·미사일 갱신 (ADR-020)
        if (e.type == EventType.MouseMove)
        {
            Vector2Int? prevTile    = hoveredTile;
            int         prevMissile = hoveredMissileId;
            Vector2 worldPos = ScreenToWorld(vp, e.mousePosition);
            hoveredTile      = TryGetTile(worldPos, out int hc, out int hr)
                ? new Vector2Int(hc, hr) : null;
            hoveredMissileId = TryHitMissileTopDown(vp, e.mousePosition, out int hm) ? hm : -1;
            if (hoveredTile != prevTile || hoveredMissileId != prevMissile) Repaint();
            return;
        }

        // 우클릭 → Brush 해제 또는 객체 즉시 삭제 (ADR-020)
        if (e.type == EventType.MouseDown && e.button == 1)
        {
            if (inPlaceMode)
            {
                inPlaceMode          = false;
                selectedPaletteIndex = -1;
                e.Use(); Repaint();
            }
            else
            {
                ApplyRightClickDelete(vp, e.mousePosition);
                e.Use(); Repaint();
            }
            return;
        }

        // 좌클릭 → 드래그 박스 시작 (Alt 제외 — Alt+드래그는 팬) (ADR-004)
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            dragStartPos = e.mousePosition;
            dragEndPos   = e.mousePosition;
            dragWasCtrl  = e.control;
            isDragging   = false;
            e.Use();
            return;
        }

        // 스크롤 휠 → 줌
        if (e.type == EventType.ScrollWheel)
        {
            viewScale = Mathf.Clamp(viewScale - e.delta.y * 0.05f, MinScale, MaxScale);
            e.Use();
            Repaint();
        }

        // 중간 버튼 / Alt+좌드래그 → 팬
        if (e.type == EventType.MouseDrag &&
            (e.button == 2 || (e.button == 0 && e.alt)))
        {
            float ppu = TilePixelSize * viewScale;
            viewOffset.x -= e.delta.x / ppu;
            viewOffset.y += e.delta.y / ppu;
            e.Use();
            Repaint();
        }
    }

    private void HandleSceneViewInput(Event e)
    {
        // 마우스 이동 → 씬뷰 호버 갱신 (Raycast)
        if (e.type == EventType.MouseMove)
        {
            Vector2Int? prevTile  = sceneHoveredTile;
            string      prevSpawn = sceneHoveredSpawnId;
            if (TryRaycastEditorScene(ViewportRect, e.mousePosition, out RaycastHit hit))
            {
                int instanceId = hit.collider.gameObject.GetInstanceID();
                if (tileByInstanceId.TryGetValue(instanceId, out Vector2Int t))
                {
                    sceneHoveredTile    = t;
                    sceneHoveredSpawnId = null;
                }
                else if (spawnByInstanceId.TryGetValue(instanceId, out string sp))
                {
                    sceneHoveredTile    = null;
                    sceneHoveredSpawnId = sp;
                }
                else
                {
                    sceneHoveredTile    = null;
                    sceneHoveredSpawnId = null;
                }
            }
            else
            {
                sceneHoveredTile    = null;
                sceneHoveredSpawnId = null;
            }
            if (sceneHoveredTile != prevTile || sceneHoveredSpawnId != prevSpawn) Repaint();
            return;
        }

        // 좌클릭 → 드래그 박스 시작 (완료·적용은 HandleInput 상단에서 처리)
        // Alt 제외 — Alt+좌드래그는 팬
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            dragStartPos = e.mousePosition;
            dragEndPos   = e.mousePosition;
            dragWasCtrl  = e.control;
            isDragging   = false;
            e.Use();
            return;
        }

        // 스크롤 휠 → 줌 (카메라 거리)
        if (e.type == EventType.ScrollWheel)
        {
            sceneCamDistance = Mathf.Clamp(sceneCamDistance + e.delta.y * 0.4f, 3f, 40f);
            UpdateSceneCameraTransform();
            e.Use();
            Repaint();
        }

        // 우클릭 드래그 → 궤도 회전 (Yaw / Pitch). 좌클릭은 미사일 배치용으로 예약 (→ ADR-003)
        if (e.type == EventType.MouseDrag && e.button == 1)
        {
            sceneCamYaw   = (sceneCamYaw + e.delta.x * 0.35f) % 360f;
            sceneCamPitch =  Mathf.Clamp(sceneCamPitch + e.delta.y * 0.35f, 5f, 89f);
            UpdateSceneCameraTransform();
            e.Use();
            Repaint();
        }

        // 중간 버튼 / Alt+좌드래그 → 카메라 기준 팬
        // 회전 후에도 팬 방향이 직관적이도록 camRight + XZ-투영 forward 기준으로 이동 (→ ADR-003)
        if (e.type == EventType.MouseDrag &&
            (e.button == 2 || (e.button == 0 && e.alt)))
        {
            float   sensitivity = sceneCamDistance * 0.003f;
            Vector3 camRight    = sceneCamera.transform.right;
            Vector3 flatForward = Vector3.ProjectOnPlane(sceneCamera.transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.001f) flatForward.Normalize();
            sceneCamTarget += (-e.delta.x * camRight + e.delta.y * flatForward) * sensitivity;
            UpdateSceneCameraTransform();
            e.Use();
            Repaint();
        }
    }

    // ── 단일 클릭 선택 ────────────────────────────────────────────────────────

    // 탑뷰 단일 클릭 — 통합 선택 (ADR-018 개정 3): 레이어 없이 우선순위 자동 판별
    // 우선순위: Missile > SpawnPoint > Tile
    private void ApplySingleClickTopDown(Rect vp, Vector2 mousePos, bool ctrl)
    {
        Vector2 worldPos = ScreenToWorld(vp, mousePos);

        // Brush 모드: 선택 대신 미사일 배치. 모드 유지 (계속 배치 가능). (ADR-017 개정 1)
        if (inPlaceMode)
        {
            if (placingType == PlacedMissileType.Hover)
            {
                if (TryGetSpawnPoint(worldPos, out string placeSpawnId))
                    PlaceMissileAtSpawn(placingType, placingPrefabIndex, placeSpawnId);
            }
            else if (placingType == PlacedMissileType.Grand)
            {
                if (TryGetTile(worldPos, out int pc, out int pr))
                    PlaceMissileAt(placingType, placingPrefabIndex, new Vector2Int(pc, pr), 0);
                else if (TryGetSpawnPoint(worldPos, out string grandSpawnId))
                    PlaceGrandHorizontalAtSpawn(placingPrefabIndex, grandSpawnId);
            }
            else
            {
                if (TryGetTile(worldPos, out int pc, out int pr))
                    PlaceMissileAt(placingType, placingPrefabIndex, new Vector2Int(pc, pr), 0);
            }
            RefreshOverlapList(vp, mousePos);
            return;
        }

        // ── 통합 선택: 클릭 위치 우선순위 자동 판별 ─────────────────────
        bool hitMissile = TryHitMissileTopDown(vp, mousePos, out int hitMissileId);
        bool hitSpawn   = TryGetSpawnPoint(worldPos, out string hitSpawnId);
        bool hitTile    = TryGetTile(worldPos, out int hitCol, out int hitRow);

        if (hitMissile)
        {
            // 미사일 선택/해제 (겹침 시 순환 선택)
            if (ctrl)
            {
                // Ctrl+클릭: 최상단 미사일 토글 (순환 없음)
                if (!selectedMissileIds.Remove(hitMissileId)) selectedMissileIds.Add(hitMissileId);
            }
            else
            {
                var hits = GetAllHitsTopDown(vp, mousePos);
                RefreshOverlapList(vp, mousePos);

                if (hits.Count == 1)
                {
                    if (selectedMissileIds.Count == 1 && selectedMissileIds.Contains(hits[0]))
                        selectedMissileIds.Remove(hits[0]);
                    else
                    {
                        selectedMissileIds.Clear();
                        selectedMissileIds.Add(hits[0]);
                    }
                }
                else
                {
                    // 겹침: 현재 선택 기준으로 다음 미사일로 순환
                    int currentIndex = -1;
                    if (selectedMissileIds.Count == 1)
                    {
                        int sel = -1;
                        foreach (var id in selectedMissileIds) sel = id;
                        currentIndex = hits.IndexOf(sel);
                    }
                    int nextIndex = (currentIndex + 1) % hits.Count;
                    selectedMissileIds.Clear();
                    selectedMissileIds.Add(hits[nextIndex]);
                }
            }
        }
        else if (hitSpawn)
        {
            // 스폰포인트 선택/해제 — 미사일 선택 초기화
            if (ctrl)
            {
                if (!selectedSpawnPoints.Remove(hitSpawnId)) selectedSpawnPoints.Add(hitSpawnId);
            }
            else if (selectedSpawnPoints.Contains(hitSpawnId))
                selectedSpawnPoints.Remove(hitSpawnId);
            else
            {
                selectedSpawnPoints.Clear();
                selectedTiles.Clear();
                selectedSpawnPoints.Add(hitSpawnId);
            }
            overlapListIds.Clear();
            selectedMissileIds.Clear();
        }
        else if (hitTile)
        {
            // 타일 선택/해제 — 미사일 선택 초기화
            var coord = new Vector2Int(hitCol, hitRow);
            if (ctrl)
            {
                if (!selectedTiles.Remove(coord)) selectedTiles.Add(coord);
            }
            else if (selectedTiles.Contains(coord))
                selectedTiles.Remove(coord);
            else
            {
                selectedTiles.Clear();
                selectedSpawnPoints.Clear();
                selectedTiles.Add(coord);
            }
            overlapListIds.Clear();
            selectedMissileIds.Clear();
        }
        // 그리드 밖 + 스폰포인트 아님: 선택 유지 (ADR-004)
    }

    // 씬뷰 단일 클릭 선택 — Physics.Raycast 기반 (ADR-007)
    private void ApplySingleClickSceneView(Rect vp, Vector2 mousePos, bool ctrl)
    {
        if (!TryRaycastEditorScene(vp, mousePos, out RaycastHit hit)) return;
        // 미스: 선택 유지 (ADR-004)

        int id = hit.collider.gameObject.GetInstanceID();

        // Brush 모드: Raycast 결과(타일/스폰포인트)에 미사일 배치 (ADR-017 개정 1)
        if (inPlaceMode)
        {
            if (placingType == PlacedMissileType.Hover)
            {
                if (spawnByInstanceId.TryGetValue(id, out string placeSpawnId))
                    PlaceMissileAtSpawn(placingType, placingPrefabIndex, placeSpawnId);
            }
            else if (placingType == PlacedMissileType.Grand)
            {
                if (tileByInstanceId.TryGetValue(id, out Vector2Int placeTile))
                    PlaceMissileAt(placingType, placingPrefabIndex, placeTile, 0);
                else if (spawnByInstanceId.TryGetValue(id, out string grandSpawnId))
                    PlaceGrandHorizontalAtSpawn(placingPrefabIndex, grandSpawnId);
            }
            else
            {
                if (tileByInstanceId.TryGetValue(id, out Vector2Int placeTile))
                    PlaceMissileAt(placingType, placingPrefabIndex, placeTile, 0);
            }
            return;
        }

        // 선택
        if (tileByInstanceId.TryGetValue(id, out Vector2Int coord))
        {
            if (ctrl)
            {
                if (!selectedTiles.Remove(coord)) selectedTiles.Add(coord);
            }
            else if (selectedTiles.Contains(coord))
                selectedTiles.Remove(coord);
            else
            {
                selectedTiles.Clear();
                selectedSpawnPoints.Clear();
                selectedTiles.Add(coord);
            }
        }
        else if (spawnByInstanceId.TryGetValue(id, out string spawnId))
        {
            if (ctrl)
            {
                if (!selectedSpawnPoints.Remove(spawnId)) selectedSpawnPoints.Add(spawnId);
            }
            else if (selectedSpawnPoints.Contains(spawnId))
                selectedSpawnPoints.Remove(spawnId);
            else
            {
                selectedSpawnPoints.Clear();
                selectedTiles.Clear();
                selectedSpawnPoints.Add(spawnId);
            }
        }
    }

    // ── 드래그 박스 선택 ──────────────────────────────────────────────────────

    // 탑뷰 박스 선택: 레이어 구분 없이 범위 내 타일·미사일 모두 선택 (ADR-018 개정 3)
    private void ApplyBoxSelectionTopDown(Rect vp, Vector2 start, Vector2 end, bool additive)
    {
        Rect box = GetBoxRect(start, end);

        if (!additive)
        {
            selectedTiles.Clear();
            selectedSpawnPoints.Clear();
            selectedMissileIds.Clear();
        }

        // 타일 선택
        for (int row = 0; row < GridRows; row++)
        for (int col = 0; col < GridCols; col++)
        {
            Vector2 center = new Vector2(-GridCols * 0.5f + col + 0.5f, GridRows * 0.5f - row - 0.5f);
            if (box.Contains(WorldToScreen(vp, center)))
                selectedTiles.Add(new Vector2Int(col, row));
        }

        // 스폰포인트 선택
        foreach ((string id, Vector2 pos) in GetAllSpawnPointDefs())
            if (box.Contains(WorldToScreen(vp, pos)))
                selectedSpawnPoints.Add(id);

        // 미사일 선택 (중심이 박스 안에 있는 것)
        foreach (var m in placedMissiles)
        {
            Vector2 c = m.Type == PlacedMissileType.Hover
                ? WorldToScreen(vp, GetSpawnPointWorldPos(m.SpawnId))
                : WorldToScreen(vp, TileCenter(m.TilePos));
            if (box.Contains(c)) selectedMissileIds.Add(m.Id);
        }
    }

    // 씬뷰 박스 선택: tileWorldPositions / spawnWorldPositions 기준 화면 투영 (ADR-007)
    private void ApplyBoxSelectionSceneView(Rect vp, Vector2 start, Vector2 end, bool additive)
    {
        if (!sceneInitialized || sceneCamera == null) return;
        Rect box = GetBoxRect(start, end);

        if (!additive) { selectedTiles.Clear(); selectedSpawnPoints.Clear(); }
        foreach (var kvp in tileWorldPositions)
            if (IsWorldPosInScreenBox(vp, kvp.Value, box)) selectedTiles.Add(kvp.Key);
        foreach (var kvp in spawnWorldPositions)
            if (IsWorldPosInScreenBox(vp, kvp.Value, box)) selectedSpawnPoints.Add(kvp.Key);
    }

    private bool IsWorldPosInScreenBox(Rect vp, Vector3 worldPos, Rect box)
    {
        Vector3 vpp = sceneCamera.WorldToViewportPoint(worldPos);
        if (vpp.z <= 0f) return false;
        return box.Contains(new Vector2(vp.x + vpp.x * vp.width, vp.y + (1f - vpp.y) * vp.height));
    }

    // ── 드래그 공통 유틸 ──────────────────────────────────────────────────────

    private void CancelDrag() { dragStartPos = null; isDragging = false; }

    private static Rect GetBoxRect(Vector2 a, Vector2 b) =>
        new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                 Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));

    // 탑뷰 전체 스폰포인트 위치 정의 — DrawSpawnPoints 상수와 동일
    private static (string id, Vector2 worldPos)[] GetAllSpawnPointDefs()
    {
        const float hc  = GridCols * 0.5f;          // 5
        const float hr  = GridRows * 0.5f;          // 5
        const float cd  = hr - 0.5f + SpawnOffset;  // 14.5 (cardinal)
        const float dx  = hc - 1f   + SpawnOffset;  // 14   (diagX)
        const float dz  = hr         + SpawnOffset;  // 15   (diagZ north)
        const float dzs = hr - 1f   + SpawnOffset;  // 14   (diagZ south)

        var defs = new (string, Vector2)[GridCols * 4 + 4];
        int i = 0;
        for (int n = 0; n < GridCols; n++)
        {
            defs[i++] = ($"N:{n}", new Vector2(-hc + n + 0.5f,  cd));
            defs[i++] = ($"S:{n}", new Vector2(-hc + n + 0.5f, -cd));
            defs[i++] = ($"E:{n}", new Vector2( cd,  hr - n - 0.5f));
            defs[i++] = ($"W:{n}", new Vector2(-cd,  hr - n - 0.5f));
        }
        defs[i++] = ("NE", new Vector2( dx,  dz));
        defs[i++] = ("NW", new Vector2(-dz,  dz));
        defs[i++] = ("SE", new Vector2( dx, -dzs));
        defs[i  ] = ("SW", new Vector2(-dz, -dzs));
        return defs;
    }

    // ── 삭제 (ADR-020) ────────────────────────────────────────────────────────

    // Delete 키: 선택된 미사일 삭제. 미사일 없으면 선택된 타일/스폰 위의 미사일 삭제.
    private void DeleteSelectedObjects()
    {
        if (selectedMissileIds.Count > 0)
        {
            foreach (int id in selectedMissileIds)
                DestroyMissileGhost(id);
            placedMissiles.RemoveAll(m => selectedMissileIds.Contains(m.Id));
            overlapListIds.RemoveAll(id => placedMissiles.FindIndex(m => m.Id == id) < 0);
            selectedMissileIds.Clear();
            Repaint();
            return;
        }
        bool changed = false;
        foreach (Vector2Int tile in selectedTiles)
        {
            for (int i = 0; i < placedMissiles.Count; i++)
                if (placedMissiles[i].Type != PlacedMissileType.Hover && placedMissiles[i].TilePos == tile)
                    DestroyMissileGhost(placedMissiles[i].Id);
            int before = placedMissiles.Count;
            RemoveMissilesAtTile(tile);
            if (placedMissiles.Count != before) changed = true;
        }
        foreach (string spawnId in selectedSpawnPoints)
        {
            for (int i = 0; i < placedMissiles.Count; i++)
                if (placedMissiles[i].Type == PlacedMissileType.Hover && placedMissiles[i].SpawnId == spawnId)
                    DestroyMissileGhost(placedMissiles[i].Id);
            int before = placedMissiles.Count;
            RemoveMissilesAtSpawn(spawnId);
            if (placedMissiles.Count != before) changed = true;
        }
        if (changed) Repaint();
    }

    // 우클릭: 커서 아래 객체 즉시 삭제. 우선순위 Missile > SpawnPoint > Tile (ADR-020)
    private void ApplyRightClickDelete(Rect vp, Vector2 mousePos)
    {
        Vector2 worldPos = ScreenToWorld(vp, mousePos);
        if (TryHitMissileTopDown(vp, mousePos, out int hitId))
        {
            DestroyMissileGhost(hitId);
            placedMissiles.RemoveAll(m => m.Id == hitId);
            selectedMissileIds.Remove(hitId);
            overlapListIds.RemoveAll(id => placedMissiles.FindIndex(m => m.Id == id) < 0);
        }
        else if (TryGetSpawnPoint(worldPos, out string spawnId))
        {
            for (int i = 0; i < placedMissiles.Count; i++)
                if (placedMissiles[i].Type == PlacedMissileType.Hover && placedMissiles[i].SpawnId == spawnId)
                    DestroyMissileGhost(placedMissiles[i].Id);
            RemoveMissilesAtSpawn(spawnId);
        }
        else if (TryGetTile(worldPos, out int col, out int row))
        {
            var tile = new Vector2Int(col, row);
            for (int i = 0; i < placedMissiles.Count; i++)
                if (placedMissiles[i].Type != PlacedMissileType.Hover && placedMissiles[i].TilePos == tile)
                    DestroyMissileGhost(placedMissiles[i].Id);
            RemoveMissilesAtTile(tile);
        }
    }

    // ── Undo / Redo ───────────────────────────────────────────────────────────
    // HashSet은 Unity 직렬화 불가 → Undo.RecordObject 대신 세션 내 자체 스택 사용 (ADR-009 예정)
    // 선택 변경 직전에 PushUndo() 호출. Ctrl+Z/Y로 탑뷰·씬뷰 선택 상태 되돌리기 가능.

    // 선택 변경 직전에 호출 — 현재 상태를 undoStack에 스냅샷, redoStack 초기화
    private void PushUndo()
    {
        undoStack.Push((new HashSet<Vector2Int>(selectedTiles), new HashSet<string>(selectedSpawnPoints)));
        redoStack.Clear();
    }

    private void UndoSelection()
    {
        if (undoStack.Count == 0) return;
        redoStack.Push((new HashSet<Vector2Int>(selectedTiles), new HashSet<string>(selectedSpawnPoints)));
        RestoreSelection(undoStack.Pop());
    }

    private void RedoSelection()
    {
        if (redoStack.Count == 0) return;
        undoStack.Push((new HashSet<Vector2Int>(selectedTiles), new HashSet<string>(selectedSpawnPoints)));
        RestoreSelection(redoStack.Pop());
    }

    private void RestoreSelection((HashSet<Vector2Int> tiles, HashSet<string> spawns) state)
    {
        selectedTiles.Clear();
        foreach (var t in state.tiles)  selectedTiles.Add(t);
        selectedSpawnPoints.Clear();
        foreach (var s in state.spawns) selectedSpawnPoints.Add(s);
        Repaint();
    }
    #endregion

    #region Simulation Controls
    // TogglePlay: 끝에서 재생 시작하면 처음부터. isPlaying 전환.
    private void TogglePlay()
    {
        if (!isPlaying && currentTime >= totalDuration) currentTime = 0f;
        isPlaying = !isPlaying;
        Repaint();
    }

    // SpeedPresets 순환 — 0.25x → 0.5x → 1x → 2x → Custom → 0.25x
    private void CycleSpeed()
    {
        speedIndex = (speedIndex + 1) % (SpeedPresets.Length + 1);
        if (speedIndex < SpeedPresets.Length)
            playSpeed = SpeedPresets[speedIndex];
        else
        {
            playSpeed = savedCustomSpeed;     // 마지막으로 입력한 커스텀 값 복원 (프리셋 순환 후 재진입해도 유지)
            pendingCustomFieldFocus = true;   // FloatField 자동 포커스 예약
        }
        Repaint();
    }

    // 이전 구간 시작점으로 점프 (구간 없으면 t=0)
    private void JumpToPrevSegmentOrStart()
    {
        float prevTime = 0f;
        foreach (var seg in globalSegments)
            if (seg.Start < currentTime - 0.01f)
                prevTime = Mathf.Max(prevTime, seg.Start);
        currentTime = prevTime;
        Repaint();
    }

    // 다음 구간 시작점으로 점프 (구간 없으면 totalDuration)
    private void JumpToNextSegmentOrEnd()
    {
        float nextTime = totalDuration;
        foreach (var seg in globalSegments)
            if (seg.Start > currentTime + 0.01f)
                nextTime = Mathf.Min(nextTime, seg.Start);
        currentTime = nextTime;
        Repaint();
    }

    // 현재 시간 위치에 전체 구간 추가 (기본 길이 DefaultSegmentDuration)
    private void AddGlobalSegment()
    {
        float start = currentTime;
        float end   = Mathf.Min(currentTime + DefaultSegmentDuration, totalDuration);
        globalSegments.Add(new GlobalSegment { Start = start, End = end });
        selectedSegmentIndex = globalSegments.Count - 1;
        Repaint();
    }
    #endregion

    #region Scene View Lifecycle
    private void SwitchMode(ViewMode newMode)
    {
        if (currentMode == newMode) return;
        currentMode = newMode;

        if (currentMode == ViewMode.SceneView)
            InitSceneView();
        else
            CleanupSceneView();

        Repaint();
    }

    private void InitSceneView()
    {
        if (sceneInitialized) return;

        // 에디터 전용 임시 씬 생성 (Additive — PlayScene을 건드리지 않음)
        editorScene      = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        editorScene.name = "MissilePatternEditor_Preview";

        // 카메라 생성 — PlayScene 메인 카메라와 동일한 FOV / 각도 사용
        var cameraGO = new GameObject("PreviewCamera");
        SceneManager.MoveGameObjectToScene(cameraGO, editorScene);
        sceneCamera                 = cameraGO.AddComponent<Camera>();
        sceneCamera.fieldOfView     = 60f;
        sceneCamera.nearClipPlane   = 0.01f;
        sceneCamera.farClipPlane    = 1000f;
        sceneCamera.clearFlags      = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = ViewportBackground;

        // 방향광 생성 (기본 조명)
        var lightGO = new GameObject("PreviewLight");
        SceneManager.MoveGameObjectToScene(lightGO, editorScene);
        var light       = lightGO.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 플랫폼 타일 배치 — Platform.cs와 동일하게 실제 메시 bounds로 간격 결정
        var tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformTilePath);
        if (tilePrefab != null)
        {
            // 피벗 → 시각적 중심 XZ 오프셋 (bounds.center).
            // GrassTile 메시는 로컬 X[-2,0] Z[0,2]에 위치해 피벗이 중심이 아니므로
            // tileWorldPositions에 피벗 대신 시각적 중심을 저장해 마커 정렬을 맞춤.
            tileMeshCenterOffset = Vector2.zero;
            var meshFilter = tilePrefab.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var bounds           = meshFilter.sharedMesh.bounds;
                tileXSize            = bounds.size.x;
                tileZSize            = bounds.size.z;
                tileSurfaceY         = bounds.max.y;   // 타일 윗면 Y (tilePos.y=0 기준)
                tileMeshCenterOffset = new Vector2(bounds.center.x, bounds.center.z);
            }

            // 플랫폼 중앙을 (0,0,0)에 맞추기 위한 tile(row=0,col=0) 기준 좌표
            // MissileSpawner의 GetTile(0).position 에 해당
            platformOrigin = new Vector3(
                -(GridCols - 1) * 0.5f * tileXSize,
                0f,
                 (GridRows - 1) * 0.5f * tileZSize);

            for (int row = 0; row < GridRows; row++)
            for (int col = 0; col < GridCols; col++)
            {
                var tilePos = new Vector3(
                    platformOrigin.x + col * tileXSize,
                    0f,
                    platformOrigin.z - row * tileZSize);
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, editorScene);
                tile.transform.position = tilePos;
                var coord = new Vector2Int(col, row);
                // 피벗이 아닌 시각적 중심(pivot + meshCenter)을 저장 — 마커 정렬용
                tileWorldPositions[coord] = new Vector3(
                    tilePos.x + tileMeshCenterOffset.x,
                    0f,
                    tilePos.z + tileMeshCenterOffset.y);
                tileByInstanceId[tile.GetInstanceID()] = coord;
            }
        }

        // 양면 메시 — 위험범위 시각화용, 한 번만 생성 후 재사용
        if (discMesh == null) discMesh = CreateDoubleSidedDiscMesh();
        if (quadMesh == null) quadMesh = CreateDoubleSidedQuadMesh();

        CreateSpawnPointObjects();
        sceneInitialized = true;  // SyncAllMissileGhosts 전에 설정 — SpawnMissileGhost 내부 가드 통과를 위해
        SyncAllMissileGhosts();   // 씬뷰 진입 시 이미 배치된 미사일 ghost 일괄 생성
        UpdateSceneCameraTransform();
    }

    private void CleanupSceneView()
    {
        if (!sceneInitialized) return;

        if (renderTexture != null)
        {
            if (sceneCamera != null) sceneCamera.targetTexture = null;
            renderTexture.Release();
            DestroyImmediate(renderTexture);
            renderTexture = null;
        }

        if (editorScene.IsValid())
            EditorSceneManager.CloseScene(editorScene, true);   // 씬 닫힘 → ghost GO 자동 파괴

        foreach (Material mat in spawnMaterials)
            if (mat != null) DestroyImmediate(mat);
        spawnMaterials.Clear();

        foreach (Material mat in missileMaterials)
            if (mat != null) DestroyImmediate(mat);
        missileMaterials.Clear();
        missileGhosts.Clear();   // GO는 씬과 함께 파괴됨 — 딕셔너리만 초기화

        if (discMesh != null) { DestroyImmediate(discMesh); discMesh = null; }
        if (quadMesh != null) { DestroyImmediate(quadMesh); quadMesh = null; }

        tileWorldPositions.Clear();
        tileByInstanceId.Clear();
        spawnByInstanceId.Clear();
        spawnWorldPositions.Clear();
        tileXSize              = 1f;
        tileZSize              = 1f;
        tileSurfaceY           = 0f;
        tileMeshCenterOffset   = Vector2.zero;
        platformOrigin         = Vector3.zero;
        sceneCamera      = null;
        sceneInitialized = false;
    }

    private void UpdateSceneCameraTransform()
    {
        if (sceneCamera == null) return;
        Quaternion rot = Quaternion.Euler(sceneCamPitch, sceneCamYaw, 0f);
        sceneCamera.transform.SetPositionAndRotation(
            sceneCamTarget + rot * new Vector3(0f, 0f, -sceneCamDistance), rot);
    }
    #endregion

    #region Coordinate Helpers
    // 타일 (col, row)의 world 좌상단 코너
    // 좌표계: X = 동(East), Y = 북(North), 플랫폼 중심 = (0, 0)
    private static Vector2 TileTopLeft(int col, int row) =>
        new Vector2(-GridCols * 0.5f + col, GridRows * 0.5f - row);

    // 타일 중심 world 좌표 (탑뷰 미사일 도형 중심점)
    private static Vector2 TileCenter(Vector2Int tile) =>
        TileTopLeft(tile.x, tile.y) + new Vector2(0.5f, -0.5f);

    private static Vector3 ToV3(Vector2 v) => new Vector3(v.x, v.y, 0f);

    // world (X=동, Y=북) → 뷰포트 내 스크린 픽셀
    private Vector2 WorldToScreen(Rect vp, Vector2 worldPos) =>
        new Vector2(
            vp.x + vp.width  * 0.5f + (worldPos.x - viewOffset.x) * TilePixelSize * viewScale,
            vp.y + vp.height * 0.5f - (worldPos.y - viewOffset.y) * TilePixelSize * viewScale
        );

    // 탑뷰 스크린 픽셀 → world (WorldToScreen 역변환)
    private Vector2 ScreenToWorld(Rect vp, Vector2 screenPos) =>
        new Vector2(
            (screenPos.x - vp.x - vp.width  * 0.5f) / (TilePixelSize * viewScale) + viewOffset.x,
            (vp.y + vp.height * 0.5f - screenPos.y) / (TilePixelSize * viewScale) + viewOffset.y
        );

    // 탑뷰 world 좌표 → 타일 (col, row). 그리드 안에 있을 때만 true 반환
    private static bool TryGetTile(Vector2 worldPos, out int col, out int row)
    {
        col = Mathf.FloorToInt(worldPos.x + GridCols * 0.5f);
        row = Mathf.FloorToInt(GridRows  * 0.5f - worldPos.y);
        return col >= 0 && col < GridCols && row >= 0 && row < GridRows;
    }

    // 탑뷰 world 좌표 → 스폰포인트 개별 ID ("N:3", "NE" 등). 근처에 있을 때만 true 반환 (ADR-004)
    // 카디널은 "방향:인덱스" 형식으로 개별 dot을 구별. 대각선은 단일 dot이므로 방향 키만 사용.
    // 판정 반경: cardinal = 1.0 world unit (dot size 5px 기준), diagonal = 1.2 world unit (7px 기준)
    private static bool TryGetSpawnPoint(Vector2 worldPos, out string id)
    {
        const float cd  = 14.5f;              // cardinalD
        const float dx  = 14f;               // diagX
        const float dz  = 15f;               // diagZ
        const float dzs = 14f;               // diagZSouth
        const float cr  = 1.0f;              // cardinal 판정 반경
        const float dr  = 1.2f;              // diagonal 판정 반경
        const float gr  = 5.5f;              // 그리드 가장자리 (GridCols * 0.5f)
        const float hc  = GridCols * 0.5f;   // 5
        const float hr  = GridRows * 0.5f;   // 5

        id = null;
        // 대각선 먼저 (단일 dot이므로 카디널과 겹치면 대각선 우선)
        if (Mathf.Abs(worldPos.x -  dx) < dr && Mathf.Abs(worldPos.y -  dz)  < dr) { id = "NE"; return true; }
        if (Mathf.Abs(worldPos.x - -dz) < dr && Mathf.Abs(worldPos.y -  dz)  < dr) { id = "NW"; return true; }
        if (Mathf.Abs(worldPos.x -  dx) < dr && Mathf.Abs(worldPos.y - -dzs) < dr) { id = "SE"; return true; }
        if (Mathf.Abs(worldPos.x - -dz) < dr && Mathf.Abs(worldPos.y - -dzs) < dr) { id = "SW"; return true; }
        // 카디널 — 클릭 위치에서 가장 가까운 dot 인덱스를 계산해 개별 ID 생성
        // N/S: x → i = round(x + hc - 0.5),  E/W: y → i = round(hr - y - 0.5)
        if (Mathf.Abs(worldPos.y -  cd) < cr && worldPos.x > -gr && worldPos.x < gr)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(worldPos.x + hc - 0.5f), 0, GridCols - 1);
            id = $"N:{i}"; return true;
        }
        if (Mathf.Abs(worldPos.y - -cd) < cr && worldPos.x > -gr && worldPos.x < gr)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(worldPos.x + hc - 0.5f), 0, GridCols - 1);
            id = $"S:{i}"; return true;
        }
        if (Mathf.Abs(worldPos.x -  cd) < cr && worldPos.y > -gr && worldPos.y < gr)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(hr - worldPos.y - 0.5f), 0, GridRows - 1);
            id = $"E:{i}"; return true;
        }
        if (Mathf.Abs(worldPos.x - -cd) < cr && worldPos.y > -gr && worldPos.y < gr)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(hr - worldPos.y - 0.5f), 0, GridRows - 1);
            id = $"W:{i}"; return true;
        }
        return false;
    }

    // 씬뷰 마우스 포지션 → 에디터 씬 오브젝트(타일/스폰포인트) 레이캐스트
    // tileByInstanceId 또는 spawnByInstanceId에 등록된 오브젝트에만 히트를 인정
    // XZ 평면 레이캐스트 방식 대신 Physics.Raycast 사용 — 타일 밖 영역 클릭 문제 해결
    private bool TryRaycastEditorScene(Rect vp, Vector2 mousePos, out RaycastHit hit)
    {
        hit = default;
        if (sceneCamera == null) return false;

        Vector2 local      = mousePos - new Vector2(vp.x, vp.y);
        Vector2 normalized = new Vector2(local.x / vp.width, 1f - local.y / vp.height);
        Ray     ray        = sceneCamera.ViewportPointToRay(new Vector3(normalized.x, normalized.y, 0f));

        if (!Physics.Raycast(ray, out hit, 1000f)) return false;

        int id = hit.collider.gameObject.GetInstanceID();
        return tileByInstanceId.ContainsKey(id) || spawnByInstanceId.ContainsKey(id);
    }

    private void ResetView()
    {
        if (currentMode == ViewMode.SceneView)
        {
            sceneCamTarget   = Vector3.zero;
            sceneCamDistance = 12f;
            sceneCamPitch    = 60f;
            sceneCamYaw      = 0f;
            UpdateSceneCameraTransform();
            Repaint();
            return;
        }

        viewOffset = Vector2.zero;
        Rect  vp       = ViewportRect;
        float fullSize = FullViewHalfExtent * 2f * TilePixelSize;
        viewScale = Mathf.Clamp(Mathf.Min(vp.width, vp.height) / fullSize * 0.9f, MinScale, MaxScale);
        Repaint();
    }
    #endregion
}
