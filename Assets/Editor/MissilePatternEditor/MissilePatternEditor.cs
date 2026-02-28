using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class MissilePatternEditor : EditorWindow
{
    #region Enums
    private enum ViewMode { TopDown, SceneView }
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

    #region Colors
    private static readonly Color ViewportBackground      = new Color(0.13f, 0.13f, 0.13f);
    private static readonly Color ToolbarBackground       = new Color(0.28f, 0.18f, 0.08f);
    private static readonly Color PlaybarBackground       = new Color(0.08f, 0.22f, 0.08f);
    private static readonly Color GridLineColor           = new Color(0.32f, 0.32f, 0.32f);
    private static readonly Color TileColor               = new Color(0.19f, 0.19f, 0.19f);
    private static readonly Color BorderColor             = new Color(0.65f, 0.65f, 0.65f);
    private static readonly Color SpawnPointCardinalColor = new Color(0.35f, 0.65f, 1.00f);  // 파랑 — N/S/E/W
    private static readonly Color SpawnPointDiagonalColor = new Color(1.00f, 0.65f, 0.25f);  // 주황 — NE/NW/SE/SW
    #endregion

    #region Top-Down Viewport State
    private Vector2 viewOffset      = Vector2.zero;
    private float   viewScale       = 1f;
    private bool    viewInitialized = false;
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
    #endregion

    [MenuItem("Window/Missile Pattern")]
    private static void ShowWindow() =>
        GetWindow<MissilePatternEditor>("Missile Pattern Editor");

    private void OnEnable()
    {
        if (currentMode == ViewMode.SceneView)
            InitSceneView();
    }

    private void OnDisable()
    {
        CleanupSceneView();
    }

    private void OnGUI()
    {
        if (!viewInitialized && currentMode == ViewMode.TopDown)
        {
            ResetView();
            viewInitialized = true;
        }

        DrawViewport();
        DrawToolbar();
        DrawPlaybar();
        HandleInput();
    }

    #region Toolbar
    private void DrawToolbar()
    {
        Rect r = new Rect(0, 0, position.width, ToolbarHeight);
        EditorGUI.DrawRect(r, ToolbarBackground);

        GUILayout.BeginArea(r);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Missile Pattern Editor", EditorStyles.boldLabel, GUILayout.ExpandHeight(true));
        GUILayout.FlexibleSpace();

        EditorGUI.BeginChangeCheck();
        int newModeIndex = GUILayout.Toolbar(
            (int)currentMode,
            new[] { "탑뷰", "씬뷰" },
            GUILayout.Height(24), GUILayout.Width(120));
        if (EditorGUI.EndChangeCheck())
            SwitchMode((ViewMode)newModeIndex);

        GUILayout.Space(8);
        if (GUILayout.Button("Reset View", GUILayout.Height(ToolbarHeight)))
            ResetView();
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
            return;
        }

        if (Event.current.type != EventType.Repaint) return;
        DrawGridArea(vp);
        DrawTiles(vp);
        DrawBorder(vp);
        DrawSpawnPoints(vp);
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
    private void DrawTiles(Rect vp)
    {
        for (int row = 0; row < GridRows; row++)
        for (int col = 0; col < GridCols; col++)
        {
            Vector2 worldMin  = TileTopLeft(col, row);
            Vector2 worldMax  = new Vector2(worldMin.x + 1f, worldMin.y - 1f);
            Vector2 screenMin = WorldToScreen(vp, worldMin);
            Vector2 screenMax = WorldToScreen(vp, worldMax);

            EditorGUI.DrawRect(
                new Rect(screenMin.x + 1, screenMin.y + 1,
                         screenMax.x - screenMin.x - 2,
                         screenMax.y - screenMin.y - 2),
                TileColor);
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
            DrawDot(vp, new Vector2(-halfCols + i + 0.5f,  cardinalD), SpawnPointCardinalColor);  // North
            DrawDot(vp, new Vector2(-halfCols + i + 0.5f, -cardinalD), SpawnPointCardinalColor);  // South
            DrawDot(vp, new Vector2( cardinalD,  halfRows - i - 0.5f), SpawnPointCardinalColor);  // East
            DrawDot(vp, new Vector2(-cardinalD,  halfRows - i - 0.5f), SpawnPointCardinalColor);  // West
        }

        DrawDot(vp, new Vector2( diagX,  diagZ),      SpawnPointDiagonalColor, 7f);  // NE
        DrawDot(vp, new Vector2(-diagZ,  diagZ),      SpawnPointDiagonalColor, 7f);  // NW
        DrawDot(vp, new Vector2( diagX, -diagZSouth), SpawnPointDiagonalColor, 7f);  // SE
        DrawDot(vp, new Vector2(-diagZ, -diagZSouth), SpawnPointDiagonalColor, 7f);  // SW

        DrawWorldLabel(vp, new Vector2(-0.5f,  diagZ + 1f), "N");
        DrawWorldLabel(vp, new Vector2(-0.5f, -diagZ - 1f), "S");
        DrawWorldLabel(vp, new Vector2( diagZ + 1f,  0.5f), "E");
        DrawWorldLabel(vp, new Vector2(-diagZ - 1f,  0.5f), "W");
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
        DrawOrientationGizmo(vp);
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
    #endregion

    #region Playbar
    private void DrawPlaybar()
    {
        Rect r = new Rect(0, position.height - PlaybarHeight, position.width, PlaybarHeight);
        EditorGUI.DrawRect(r, PlaybarBackground);

        GUILayout.BeginArea(r);
        GUILayout.Label("── Timeline (미구현) ──",
                         EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandHeight(true));
        GUILayout.EndArea();
    }
    #endregion

    #region Input
    private void HandleInput()
    {
        Event e  = Event.current;
        Rect  vp = ViewportRect;
        if (!vp.Contains(e.mousePosition)) return;

        if (currentMode == ViewMode.SceneView)
        {
            HandleSceneViewInput(e);
            return;
        }

        // 탑뷰: 스크롤 휠 → 줌
        if (e.type == EventType.ScrollWheel)
        {
            viewScale = Mathf.Clamp(viewScale - e.delta.y * 0.05f, MinScale, MaxScale);
            e.Use();
            Repaint();
        }

        // 탑뷰: 중간 버튼 / Alt+좌드래그 → 팬
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

        // 플랫폼 타일 배치 (10×10 GrassTile)
        var tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformTilePath);
        if (tilePrefab != null)
        {
            for (int row = 0; row < GridRows; row++)
            for (int col = 0; col < GridCols; col++)
            {
                float x    = -GridCols * 0.5f + col + 0.5f;
                float z    =  GridRows * 0.5f - row - 0.5f;  // row=0 → +4.5(north), row=9 → -4.5(south)
                var   tile = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, editorScene);
                tile.transform.position = new Vector3(x, 0f, z);
            }
        }

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
