using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class MissilePatternEditor : EditorWindow
{
    #region Enums
    private enum ViewMode            { TopDown, SceneView }
    private enum InteractionMode     { Select, Erase }               // ADR-005: 플로팅 오버레이 툴바 + 단축키 S/E/Esc
    private enum SelectionLayer      { Tile, Missile }               // ADR-006: Phase 1 = Tile 전용
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
    #endregion

    #region Top-Down Viewport State
    private Vector2 viewOffset      = Vector2.zero;
    private float   viewScale       = 1f;
    private bool    viewInitialized = false;
    #endregion

    #region Interaction State
    // 현재 상호작용 모드 — 플로팅 오버레이 툴바 및 단축키로 전환 (ADR-005)
    // Select: 좌클릭 = 타일/스폰포인트 선택,  Erase: 좌클릭 = 선택 해제 (Phase 3에서 미사일 삭제)
    private InteractionMode currentInteractionMode = InteractionMode.Select;

    // 탑뷰 타일 다중 선택 — HashSet 기반 (ADR-004)
    // Ctrl+클릭: 토글, 단독 클릭: 초기화 후 단일 선택, 재클릭: 해제, 그리드 밖 클릭: 선택 유지
    private readonly HashSet<Vector2Int> selectedTiles       = new HashSet<Vector2Int>();
    // 탑뷰 스폰포인트 개별 선택 — "N:3", "E:0" (cardinal) / "NE", "SW" (diagonal) (ADR-004)
    // TODO (Phase 3): spawnId 키로 Dictionary<string, MissileData> 연결. 현재는 선택 상태만.
    private readonly HashSet<string>     selectedSpawnPoints = new HashSet<string>();
    private Vector2Int? hoveredTile = null;   // 마우스 호버 타일. null = 그리드 밖

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
    private readonly Dictionary<Vector2Int, Vector3> tileWorldPositions = new Dictionary<Vector2Int, Vector3>();
    private readonly Dictionary<int, Vector2Int>     tileByInstanceId   = new Dictionary<int, Vector2Int>();
    private readonly Dictionary<int, string>         spawnByInstanceId  = new Dictionary<int, string>();
    private readonly Dictionary<string, Vector3>     spawnWorldPositions = new Dictionary<string, Vector3>();

    // 씬뷰 플랫폼 실제 크기 정보 — Platform.cs와 동일하게 MeshFilter.bounds에서 읽음
    // platformOrigin = tile(row=0, col=0) 중심 위치 (MissileSpawner의 GetTile(0)에 해당)
    private float   tileXSize      = 1f;
    private float   tileZSize      = 1f;
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

        // ── 미사일 뷰 패널 (갈색) ──────────────────────────────────────────
        GUILayout.BeginArea(new Rect(0, 0, MissileViewW, ToolbarHeight));
        GUILayout.BeginHorizontal();
        GUILayout.Label("미사일 뷰", EditorStyles.boldLabel,
                        GUILayout.ExpandHeight(true), GUILayout.Width(72));
        GUILayout.Label("(미구현)", EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandHeight(true));
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

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
        }

        // 드래그 박스 오버레이 — 탑뷰·씬뷰 공통 (진행 중일 때만)
        if (Event.current.type == EventType.Repaint && isDragging && dragStartPos != null)
            DrawDragBox(dragStartPos.Value, dragEndPos);

        // 상호작용 모드 오버레이 — 양쪽 모드 공통, 내부에서 이벤트 타입 필터 (ADR-005)
        DrawInteractionModeOverlay(vp);
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
        DrawSceneSelectedMarker(vp);
        DrawSceneSelectedSpawnMarkers(vp);
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

    // 스폰포인트 큐브 오브젝트 일괄 생성 — InitSceneView에서 한 번만 호출
    // MissileSpawner.CreateSpawnPoint() 를 에디터 씬에 그대로 재현.
    // platformOrigin = GetTile(0) 위치, tileXSize/tileZSize = 실제 메시 크기.
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

    // 상호작용 모드 플로팅 오버레이 툴바 — 뷰포트 좌상단, 탑뷰·씬뷰 공통 (ADR-005)
    // Repaint: 버튼 렌더, MouseDown: 클릭 감지. 그 외 이벤트는 스킵.
    // 단축키 S/E/Esc는 HandleInput에서 처리.
    private void DrawInteractionModeOverlay(Rect vp)
    {
        Event e = Event.current;
        if (e.type != EventType.Repaint && e.type != EventType.MouseDown) return;

        const float BtnSize = 32f;
        const float Pad     =  4f;
        const float Margin  =  8f;

        var modes = new (InteractionMode mode, string label, string tooltip)[]
        {
            (InteractionMode.Select, "S", "선택 (단축키: S)"),
            (InteractionMode.Erase,  "E", "지우기 (단축키: E)"),
        };

        float panelW = BtnSize + Pad * 2;
        float panelH = modes.Length * BtnSize + (modes.Length + 1) * Pad;
        var   panel  = new Rect(vp.x + Margin, vp.y + Margin, panelW, panelH);

        if (e.type == EventType.Repaint)
            EditorGUI.DrawRect(panel, new Color(0.12f, 0.12f, 0.12f, 0.80f));

        var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 13 };

        for (int i = 0; i < modes.Length; i++)
        {
            var (mode, label, tooltip) = modes[i];
            var r = new Rect(panel.x + Pad, panel.y + Pad + i * (BtnSize + Pad), BtnSize, BtnSize);

            bool active = currentInteractionMode == mode;

            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, active
                    ? new Color(0.25f, 0.55f, 1.00f, 0.75f)
                    : new Color(0.22f, 0.22f, 0.22f, 0.60f));
                labelStyle.normal.textColor = active ? Color.white : new Color(0.70f, 0.70f, 0.70f);
                GUI.Label(r, new GUIContent(label, tooltip), labelStyle);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                currentInteractionMode = mode;
                e.Use();
                Repaint();
            }
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
            case KeyCode.S:
                currentInteractionMode = InteractionMode.Select;
                e.Use(); Repaint(); return;
            case KeyCode.E:
                currentInteractionMode = InteractionMode.Erase;
                e.Use(); Repaint(); return;
            case KeyCode.Escape:
                if (selectedTiles.Count > 0 || selectedSpawnPoints.Count > 0) PushUndo();
                selectedTiles.Clear();
                selectedSpawnPoints.Clear();
                CancelDrag();
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

        // 마우스 이동 → 호버 타일 갱신
        if (e.type == EventType.MouseMove)
        {
            Vector2Int? prev = hoveredTile;
            Vector2 worldPos = ScreenToWorld(vp, e.mousePosition);
            hoveredTile = TryGetTile(worldPos, out int hc, out int hr)
                ? new Vector2Int(hc, hr) : null;
            if (hoveredTile != prev) Repaint();
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

    // 탑뷰 단일 클릭 선택: Select=이미선택→해당만해제/미선택→초기화+단일, Erase=해당 제거 (ADR-004)
    private void ApplySingleClickTopDown(Rect vp, Vector2 mousePos, bool ctrl)
    {
        Vector2 worldPos = ScreenToWorld(vp, mousePos);

        if (currentInteractionMode == InteractionMode.Erase)
        {
            if (TryGetSpawnPoint(worldPos, out string eraseSpawn))
                selectedSpawnPoints.Remove(eraseSpawn);
            else if (TryGetTile(worldPos, out int ec, out int er))
                selectedTiles.Remove(new Vector2Int(ec, er));
            return;
        }

        // Select
        if (TryGetSpawnPoint(worldPos, out string spawnId))
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
        else if (TryGetTile(worldPos, out int sc, out int sr))
        {
            var coord = new Vector2Int(sc, sr);
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
        // 그리드 밖 + 스폰포인트 아님: 선택 유지 (ADR-004)
    }

    // 씬뷰 단일 클릭 선택 — Physics.Raycast 기반 (ADR-007)
    private void ApplySingleClickSceneView(Rect vp, Vector2 mousePos, bool ctrl)
    {
        if (!TryRaycastEditorScene(vp, mousePos, out RaycastHit hit)) return;
        // 미스: 선택 유지 (ADR-004)

        int id = hit.collider.gameObject.GetInstanceID();

        if (currentInteractionMode == InteractionMode.Erase)
        {
            if (tileByInstanceId.TryGetValue(id, out Vector2Int eraseCoord))
                selectedTiles.Remove(eraseCoord);
            else if (spawnByInstanceId.TryGetValue(id, out string eraseSpawn))
                selectedSpawnPoints.Remove(eraseSpawn);
            return;
        }

        // Select
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

    // 탑뷰 박스 선택: Select=(additive ? 추가 : 초기화+추가), Erase=박스 내 항목 제거
    private void ApplyBoxSelectionTopDown(Rect vp, Vector2 start, Vector2 end, bool additive)
    {
        Rect box = GetBoxRect(start, end);

        if (currentInteractionMode == InteractionMode.Erase)
        {
            for (int row = 0; row < GridRows; row++)
            for (int col = 0; col < GridCols; col++)
            {
                Vector2 center = new Vector2(-GridCols * 0.5f + col + 0.5f, GridRows * 0.5f - row - 0.5f);
                if (box.Contains(WorldToScreen(vp, center)))
                    selectedTiles.Remove(new Vector2Int(col, row));
            }
            foreach ((string id, Vector2 pos) in GetAllSpawnPointDefs())
                if (box.Contains(WorldToScreen(vp, pos)))
                    selectedSpawnPoints.Remove(id);
            return;
        }

        // Select
        if (!additive) { selectedTiles.Clear(); selectedSpawnPoints.Clear(); }
        for (int row = 0; row < GridRows; row++)
        for (int col = 0; col < GridCols; col++)
        {
            Vector2 center = new Vector2(-GridCols * 0.5f + col + 0.5f, GridRows * 0.5f - row - 0.5f);
            if (box.Contains(WorldToScreen(vp, center)))
                selectedTiles.Add(new Vector2Int(col, row));
        }
        foreach ((string id, Vector2 pos) in GetAllSpawnPointDefs())
            if (box.Contains(WorldToScreen(vp, pos)))
                selectedSpawnPoints.Add(id);
    }

    // 씬뷰 박스 선택: tileWorldPositions / spawnWorldPositions 기준 화면 투영 (ADR-007)
    private void ApplyBoxSelectionSceneView(Rect vp, Vector2 start, Vector2 end, bool additive)
    {
        if (!sceneInitialized || sceneCamera == null) return;
        Rect box = GetBoxRect(start, end);

        if (currentInteractionMode == InteractionMode.Erase)
        {
            foreach (var kvp in tileWorldPositions)
                if (IsWorldPosInScreenBox(vp, kvp.Value, box)) selectedTiles.Remove(kvp.Key);
            foreach (var kvp in spawnWorldPositions)
                if (IsWorldPosInScreenBox(vp, kvp.Value, box)) selectedSpawnPoints.Remove(kvp.Key);
            return;
        }

        // Select
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
            var tileMeshCenterOffset = Vector2.zero;
            var meshFilter = tilePrefab.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var bounds           = meshFilter.sharedMesh.bounds;
                tileXSize            = bounds.size.x;
                tileZSize            = bounds.size.z;
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

        CreateSpawnPointObjects();
        UpdateSceneCameraTransform();
        sceneInitialized = true;
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
            EditorSceneManager.CloseScene(editorScene, true);

        foreach (Material mat in spawnMaterials)
            if (mat != null) DestroyImmediate(mat);
        spawnMaterials.Clear();

        tileWorldPositions.Clear();
        tileByInstanceId.Clear();
        spawnByInstanceId.Clear();
        spawnWorldPositions.Clear();
        tileXSize        = 1f;
        tileZSize        = 1f;
        platformOrigin   = Vector3.zero;
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
