using UnityEngine;
using UnityEditor;

public class MissilePatternEditor : EditorWindow
{
    #region Layout Constants
    private const float ToolbarHeight      = 40f;
    private const float PlaybarHeight      = 60f;
    private const float TilePixelSize      = 40f;   // scale=1 기준 픽셀/타일
    private const float MinScale           = 0.3f;
    private const float MaxScale           = 3.0f;
    private const int   GridCols           = 10;
    private const int   GridRows           = 10;
    private const float FullViewHalfExtent = 17f;   // Reset View 시 커버할 world 반경 (스폰포인트 ~15 + 여백)
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

    #region Viewport State
    private Vector2 viewOffset    = Vector2.zero;
    private float   viewScale     = 1f;
    private bool    viewInitialized = false;
    #endregion

    [MenuItem("Window/Missile Pattern")]
    private static void ShowWindow() =>
        GetWindow<MissilePatternEditor>("Missile Pattern Editor");

    private void OnEnable()  { }
    private void OnDisable() { }

    private void OnGUI()
    {
        if (!viewInitialized)
        {
            ResetView();
            viewInitialized = true;
        }

        DrawToolbar();
        DrawViewport();
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

        if (Event.current.type != EventType.Repaint) return;

        DrawGridArea(vp);
        DrawTiles(vp);
        DrawBorder(vp);
        DrawSpawnPoints(vp);
    }

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
    // 에디터 좌표 기준 (1유닛 = 타일 1칸, 플랫폼 중심 = 0,0):
    //   N/S: 플랫폼 상하 가장자리에서 +10 (Y=±14.5), X 10개 (-5~4)
    //   E/W: 플랫폼 좌우 가장자리에서 +10 (X=±14.5), Y 10개 (5~-4)
    //   대각선: 코너에서 (+10,+10) 오프셋, 각 1개
    private void DrawSpawnPoints(Rect vp)
    {
        for (int i = 0; i < 10; i++)
        {
            DrawDot(vp, new Vector2(-5f + i,  14.5f), SpawnPointCardinalColor);  // North
            DrawDot(vp, new Vector2(-5f + i, -14.5f), SpawnPointCardinalColor);  // South
            DrawDot(vp, new Vector2( 14.5f,   5f - i), SpawnPointCardinalColor); // East
            DrawDot(vp, new Vector2(-14.5f,   5f - i), SpawnPointCardinalColor); // West
        }

        DrawDot(vp, new Vector2( 14f,  15f), SpawnPointDiagonalColor, 7f);  // NE
        DrawDot(vp, new Vector2(-15f,  15f), SpawnPointDiagonalColor, 7f);  // NW
        DrawDot(vp, new Vector2( 14f, -14f), SpawnPointDiagonalColor, 7f);  // SE
        DrawDot(vp, new Vector2(-15f, -14f), SpawnPointDiagonalColor, 7f);  // SW

        DrawWorldLabel(vp, new Vector2(-0.5f,  16f), "N");
        DrawWorldLabel(vp, new Vector2(-0.5f, -16f), "S");
        DrawWorldLabel(vp, new Vector2( 16f,   0.5f), "E");
        DrawWorldLabel(vp, new Vector2(-17f,   0.5f), "W");
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
        viewOffset = Vector2.zero;
        Rect  vp       = ViewportRect;
        float fullSize = FullViewHalfExtent * 2f * TilePixelSize;
        viewScale = Mathf.Clamp(Mathf.Min(vp.width, vp.height) / fullSize * 0.9f, MinScale, MaxScale);
        Repaint();
    }
    #endregion
}
