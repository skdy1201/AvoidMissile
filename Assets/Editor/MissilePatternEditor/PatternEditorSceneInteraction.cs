using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 씬뷰 인터랙션 — 플랫폼 타일/스폰포인트 생성, 레이캐스트 선택, Handles 시각화.
/// PatternEditorController.Open/Close에서 Initialize/Cleanup 호출.
/// Unity Scene View를 직접 활용하므로 커스텀 카메라/RenderTexture 불필요.
/// </summary>
public static class PatternEditorSceneInteraction
{
    #region Constants

    private const int    GridCols          = 10;
    private const int    GridRows          = 10;
    private const float  SpawnOffset       = 10f;
    private const string PlatformTilePath  = "Assets/Prefabs/Platform/GrassTile.prefab";
    private const string RootObjectName      = "[PatternEditor]";
    private const float  DragThreshold       = 5f;
    private const string MissilePrefabFolder = "Assets/Prefabs/Missile";

    // 스폰 높이 — GlobalData.MissileDropPoint = 50, HoverMissile Y += 2.5
    private const float FallingSpawnHeight     = 50f;
    private const float HoverSpawnYOffset      = 2.5f;
    private const float GrandHorizontalYOffset = 5.5f;   // diameter 3 기본값

    // 데칼 Y 오프셋 — FallingMissile: 1.1, GrandMissile: -0.5
    private const float FallingDecalYOffset = 1.1f;
    private const float GrandDecalYOffset   = -0.5f;

    // Grand 기본 diameter (프리팹 기본값 0 → 에디터에서는 3 사용)
    private const int DefaultGrandDiameter = 3;

    // 팔레트 UI 레이아웃
    private const float PaletteX       = 10f;
    private const float PaletteY       = 10f;
    private const float PaletteWidth   = 160f;
    private const float PaletteItemH   = 24f;
    private const float PalettePad     = 4f;
    private const float PaletteHeaderH = 22f;

    #endregion

    #region Enums

    public enum PlacedMissileType { Falling, Grand, Hover }

    #endregion

    #region Placed Missile Data

    public class PlacedMissile
    {
        public PlacedMissileType type;
        public string            locationKey;   // "T:col,row" 또는 "S:spawnId"
        public GameObject        ghost;
    }

    #endregion

    #region Colors

    private static readonly Color SelectionColor     = new Color(0.25f, 0.55f, 1.00f, 0.90f);
    private static readonly Color SelectionFillColor  = new Color(0.25f, 0.55f, 1.00f, 0.20f);
    private static readonly Color HoverColor          = new Color(1.00f, 0.85f, 0.20f, 0.90f);
    private static readonly Color HoverFillColor      = new Color(1.00f, 0.85f, 0.20f, 0.08f);
    private static readonly Color SpawnCardinalColor  = new Color(0.35f, 0.65f, 1.00f);
    private static readonly Color SpawnDiagonalColor  = new Color(1.00f, 0.65f, 0.25f);
    private static readonly Color SpawnSelectColor    = new Color(1.00f, 1.00f, 1.00f, 1.00f);
    private static readonly Color DragBoxFillColor    = new Color(0.25f, 0.55f, 1.00f, 0.15f);
    private static readonly Color DragBoxOutlineColor = new Color(0.25f, 0.55f, 1.00f, 0.60f);

    #endregion

    #region State

    private static bool initialized;

    // 루트 오브젝트 — 생성된 타일/스폰포인트의 부모. 도메인 리로드 시 이름으로 찾아 정리
    private static GameObject rootObject;

    // 타일 메시 정보
    private static float   tileXSize = 1f;
    private static float   tileZSize = 1f;
    private static float   tileSurfaceY;
    private static Vector2 tileMeshCenterOffset;
    private static Vector3 platformOrigin;

    // 룩업 테이블 — 레이캐스트 히트 판정 및 마커 위치
    private static readonly Dictionary<Vector2Int, Vector3> tileWorldPositions  = new Dictionary<Vector2Int, Vector3>();
    private static readonly Dictionary<int, Vector2Int>     tileByInstanceId    = new Dictionary<int, Vector2Int>();
    private static readonly Dictionary<int, string>         spawnByInstanceId   = new Dictionary<int, string>();
    private static readonly Dictionary<string, Vector3>     spawnWorldPositions = new Dictionary<string, Vector3>();

    // 머티리얼 추적 (Cleanup 시 파괴)
    private static readonly List<Material> createdMaterials = new List<Material>();

    // 선택 상태
    public static readonly HashSet<Vector2Int> SelectedTiles       = new HashSet<Vector2Int>();
    public static readonly HashSet<string>     SelectedSpawnPoints = new HashSet<string>();

    // 호버 상태
    private static Vector2Int? hoveredTile;
    private static string      hoveredSpawnId;

    // 드래그 박스 선택
    private static bool    isDragging;
    private static Vector2 dragStart;
    private static Vector2 dragEnd;
    private static bool    dragWasCtrl;

    // 팔레트
    private static readonly List<GameObject>        palettePrefabs = new List<GameObject>();
    private static readonly List<PlacedMissileType> paletteTypes   = new List<PlacedMissileType>();
    private static int  selectedPaletteIndex = -1;
    private static bool inPlaceMode;

    // 배치된 미사일
    private static readonly List<PlacedMissile>            placedMissiles    = new List<PlacedMissile>();
    private static readonly Dictionary<int, PlacedMissile> ghostByInstanceId = new Dictionary<int, PlacedMissile>();
    private static readonly HashSet<PlacedMissile>         selectedMissiles  = new HashSet<PlacedMissile>();

    // 배치 미리보기 (호버 고스트)
    private static GameObject hoverGhost;
    private static string     hoverGhostLocation;

    private static readonly Color MissileSelectColor = new Color(1f, 1f, 0f, 0.90f);

    #endregion

    #region Public API

    public static bool Initialized => initialized;

    public static void Initialize()
    {
        if (initialized) return;

        // 도메인 리로드 후 잔존 오브젝트 정리
        CleanupRootObject();

        rootObject = new GameObject(RootObjectName);
        rootObject.hideFlags = HideFlags.DontSave;

        CreatePlatformTiles();
        CreateSpawnPointObjects();
        ScanMissilePrefabs();

        SceneView.duringSceneGui += OnSceneGUI;
        initialized = true;

        // Scene View에 포커스하고 플랫폼 중심으로 프레이밍
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            // 플랫폼 중심 = 원점 + 그리드 절반 오프셋
            Vector3 center = new Vector3(
                platformOrigin.x + (GridCols - 1) * 0.5f * tileXSize,
                platformOrigin.y,
                platformOrigin.z - (GridRows - 1) * 0.5f * tileZSize);
            sceneView.LookAt(center, Quaternion.Euler(60f, 0f, 0f), 20f);
            sceneView.Repaint();
        }
    }

    public static void Cleanup()
    {
        SceneView.duringSceneGui -= OnSceneGUI;

        CleanupRootObject();

        foreach (var mat in createdMaterials)
            if (mat != null) Object.DestroyImmediate(mat);
        createdMaterials.Clear();

        tileWorldPositions.Clear();
        tileByInstanceId.Clear();
        spawnByInstanceId.Clear();
        spawnWorldPositions.Clear();
        SelectedTiles.Clear();
        SelectedSpawnPoints.Clear();

        hoveredTile    = null;
        hoveredSpawnId = null;
        isDragging     = false;

        palettePrefabs.Clear();
        paletteTypes.Clear();
        selectedPaletteIndex = -1;
        inPlaceMode          = false;

        ClearAllPlacedMissiles();
        DestroyHoverGhost();

        initialized    = false;
    }

    #endregion

    #region Scene Setup

    private static void CleanupRootObject()
    {
        // HideFlags.DontSave 오브젝트는 GameObject.Find로 찾을 수 있음
        var existing = GameObject.Find(RootObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);
        rootObject = null;
    }

    private static void CreatePlatformTiles()
    {
        var tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformTilePath);
        if (tilePrefab == null)
        {
            Debug.LogWarning($"[PatternEditor] 타일 프리팹을 찾을 수 없습니다: {PlatformTilePath}");
            return;
        }

        // 메시 bounds에서 타일 크기 및 피벗 오프셋 추출
        tileMeshCenterOffset = Vector2.zero;
        var meshFilter = tilePrefab.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            var bounds   = meshFilter.sharedMesh.bounds;
            tileXSize    = bounds.size.x;
            tileZSize    = bounds.size.z;
            tileSurfaceY = bounds.max.y;
            tileMeshCenterOffset = new Vector2(bounds.center.x, bounds.center.z);
        }

        // 씬에 있는 Platform 오브젝트의 실제 위치를 읽어서 게임과 동일한 좌표에 배치
        var platformGO = GameObject.Find("Platform");
        if (platformGO != null)
        {
            platformOrigin = platformGO.transform.position;
        }
        else
        {
            // Platform이 없으면 씬 파일의 기본값 사용
            platformOrigin = new Vector3(-7.5f, -10f, 9.5f);
            Debug.LogWarning("[PatternEditor] Platform 오브젝트를 찾을 수 없어 기본 위치를 사용합니다.");
        }

        for (int row = 0; row < GridRows; row++)
        for (int col = 0; col < GridCols; col++)
        {
            var tilePos = new Vector3(
                platformOrigin.x + col * tileXSize,
                platformOrigin.y,
                platformOrigin.z - row * tileZSize);

            var tile = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab);
            tile.transform.SetParent(rootObject.transform);
            tile.transform.position = tilePos;
            tile.hideFlags = HideFlags.DontSave;

            var coord = new Vector2Int(col, row);
            tileWorldPositions[coord] = new Vector3(
                tilePos.x + tileMeshCenterOffset.x,
                platformOrigin.y,
                tilePos.z + tileMeshCenterOffset.y);
            tileByInstanceId[tile.GetInstanceID()] = coord;
        }
    }

    private static void CreateSpawnPointObjects()
    {
        Material cardinalMat = CreateUnlitMaterial(SpawnCardinalColor);
        Material diagonalMat = CreateUnlitMaterial(SpawnDiagonalColor);

        Vector3 TilePos(int row, int col) => new Vector3(
            platformOrigin.x + col * tileXSize,
            platformOrigin.y,
            platformOrigin.z - row * tileZSize);

        // Cardinal — N/S/E/W
        Vector3 northBase = TilePos(0, 0);
        northBase.z += SpawnOffset;
        northBase.x -= tileXSize * 0.5f;
        for (int i = 0; i < GridCols; i++)
            CreateSpawnCube(new Vector3(northBase.x + tileXSize * i, platformOrigin.y + 0.25f, northBase.z), cardinalMat, $"N:{i}");

        Vector3 southBase = TilePos(GridRows - 1, 0);
        southBase.z -= SpawnOffset;
        southBase.x -= tileXSize * 0.5f;
        for (int i = 0; i < GridCols; i++)
            CreateSpawnCube(new Vector3(southBase.x + tileXSize * i, platformOrigin.y + 0.25f, southBase.z), cardinalMat, $"S:{i}");

        Vector3 eastBase = TilePos(0, GridCols - 1);
        eastBase.x += SpawnOffset;
        eastBase.z += tileZSize * 0.5f;
        for (int i = 0; i < GridRows; i++)
            CreateSpawnCube(new Vector3(eastBase.x, platformOrigin.y + 0.25f, eastBase.z - tileZSize * i), cardinalMat, $"E:{i}");

        Vector3 westBase = TilePos(0, 0);
        westBase.x -= SpawnOffset;
        westBase.z += tileZSize * 0.5f;
        for (int i = 0; i < GridRows; i++)
            CreateSpawnCube(new Vector3(westBase.x, platformOrigin.y + 0.25f, westBase.z - tileZSize * i), cardinalMat, $"W:{i}");

        // Diagonal — NE/NW/SE/SW
        Vector3 ne = TilePos(0, GridCols - 1);
        ne.x = ne.x - tileXSize * 0.5f + SpawnOffset;
        ne.z = ne.z + tileZSize * 0.5f + SpawnOffset;
        CreateSpawnCube(new Vector3(ne.x, platformOrigin.y + 0.25f, ne.z), diagonalMat, "NE");

        Vector3 nw = TilePos(0, 0);
        nw.x = nw.x - tileXSize * 0.5f - SpawnOffset;
        nw.z = nw.z + tileZSize * 0.5f + SpawnOffset;
        CreateSpawnCube(new Vector3(nw.x, platformOrigin.y + 0.25f, nw.z), diagonalMat, "NW");

        Vector3 se = TilePos(GridRows - 1, GridCols - 1);
        se.x = se.x - tileXSize * 0.5f + SpawnOffset;
        se.z = se.z + tileZSize * 0.5f - SpawnOffset;
        CreateSpawnCube(new Vector3(se.x, platformOrigin.y + 0.25f, se.z), diagonalMat, "SE");

        Vector3 sw = TilePos(GridRows - 1, 0);
        sw.x = sw.x - tileXSize * 0.5f - SpawnOffset;
        sw.z = sw.z + tileZSize * 0.5f - SpawnOffset;
        CreateSpawnCube(new Vector3(sw.x, platformOrigin.y + 0.25f, sw.z), diagonalMat, "SW");
    }

    private static void CreateSpawnCube(Vector3 pos, Material mat, string spawnId)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(rootObject.transform);
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 0.5f;
        go.hideFlags = HideFlags.DontSave;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        spawnByInstanceId[go.GetInstanceID()]  = spawnId;
        spawnWorldPositions[spawnId]           = pos;
    }

    private static Material CreateUnlitMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.color = color;
        createdMaterials.Add(mat);
        return mat;
    }

    private static void ScanMissilePrefabs()
    {
        palettePrefabs.Clear();
        paletteTypes.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { MissilePrefabFolder });
        foreach (string guid in guids)
        {
            string     path   = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<Missile>() == null) continue;
            if (prefab.name.EndsWith("Model")) continue;

            palettePrefabs.Add(prefab);
            paletteTypes.Add(GetTypeFromName(prefab.name));
        }
        selectedPaletteIndex = -1;
        inPlaceMode          = false;
    }

    private static PlacedMissileType GetTypeFromName(string prefabName)
    {
        if (prefabName.Contains("Grand"))  return PlacedMissileType.Grand;
        if (prefabName.Contains("Homing")) return PlacedMissileType.Hover;
        return PlacedMissileType.Falling;
    }

    #endregion

    #region SceneView Callback

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!initialized) return;

        Event e = Event.current;

        HandleInput(e);
        DrawHoverHighlight();
        DrawSelectionMarkers();
        DrawSpawnSelectionMarkers();
        DrawMissileSelectionMarkers();
        UpdateHoverGhost();
        DrawPalette(sceneView);

        if (isDragging)
            DrawDragBox();
    }

    #endregion

    #region Input

    private static bool IsMouseOverPalette(Vector2 mousePos)
    {
        float totalH = PaletteHeaderH + PalettePad + palettePrefabs.Count * (PaletteItemH + PalettePad);
        Rect panelRect = new Rect(PaletteX, PaletteY, PaletteWidth, totalH);
        return panelRect.Contains(mousePos);
    }

    private static void HandleInput(Event e)
    {
        // 호버 — 마우스 이동 시 레이캐스트
        if (e.type == EventType.MouseMove)
        {
            UpdateHover();
            e.Use();
            return;
        }

        // 팔레트 영역 내 클릭은 월드 인터랙션 무시 (팔레트 UI가 처리)
        if (e.isMouse && IsMouseOverPalette(e.mousePosition))
            return;

        // 좌클릭 다운 — 드래그 시작 (Alt 제외: Alt+좌드래그는 Scene View 오빗)
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            dragStart   = e.mousePosition;
            dragEnd     = e.mousePosition;
            dragWasCtrl = e.control;
            isDragging  = false;

            // Scene View 기본 동작(오브젝트 선택) 방지
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.hotControl = controlId;
            e.Use();
            return;
        }

        // 좌드래그 — 드래그 박스 갱신
        if (e.type == EventType.MouseDrag && e.button == 0 && !e.alt)
        {
            dragEnd = e.mousePosition;
            if (!isDragging && Vector2.Distance(dragStart, dragEnd) > DragThreshold)
                isDragging = true;
            e.Use();
            return;
        }

        // 좌클릭 업 — 단일 클릭 또는 박스 선택 적용
        if (e.type == EventType.MouseUp && e.button == 0 && !e.alt)
        {
            GUIUtility.hotControl = 0;

            if (isDragging)
            {
                ApplyBoxSelection(dragWasCtrl);
                isDragging = false;
            }
            else
            {
                ApplySingleClick(e.control);
            }
            e.Use();
            return;
        }

        // Delete 키 — 선택된 미사일 삭제
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
        {
            if (selectedMissiles.Count > 0)
            {
                RemoveSelectedMissiles();
                SceneView.RepaintAll();
                e.Use();
                return;
            }
        }

        // Escape 키 — 선택 해제 / Place 모드 종료
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            if (inPlaceMode)
            {
                selectedPaletteIndex = -1;
                inPlaceMode          = false;
            }
            ClearAllSelections();
            SceneView.RepaintAll();
            e.Use();
        }

        // 우클릭 — Place 모드 중이면 모드 해제, 아니면 선택된 미사일 삭제
        if (e.type == EventType.MouseDown && e.button == 1 && !e.alt)
        {
            if (inPlaceMode)
            {
                selectedPaletteIndex = -1;
                inPlaceMode          = false;
                SceneView.RepaintAll();
                e.Use();
            }
            else if (selectedMissiles.Count > 0)
            {
                RemoveSelectedMissiles();
                SceneView.RepaintAll();
                e.Use();
            }
        }
    }

    private static void UpdateHover()
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            int id = hit.collider.gameObject.GetInstanceID();
            if (tileByInstanceId.TryGetValue(id, out Vector2Int t))
            {
                hoveredTile    = t;
                hoveredSpawnId = null;
            }
            else if (spawnByInstanceId.TryGetValue(id, out string sp))
            {
                hoveredTile    = null;
                hoveredSpawnId = sp;
            }
            else
            {
                hoveredTile    = null;
                hoveredSpawnId = null;
            }
        }
        else
        {
            hoveredTile    = null;
            hoveredSpawnId = null;
        }

        SceneView.RepaintAll();
    }

    private static void ApplySingleClick(bool ctrl)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(dragStart);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // 빈 공간 클릭 — Place 모드 해제
            if (inPlaceMode && !ctrl)
            {
                selectedPaletteIndex = -1;
                inPlaceMode          = false;
            }
            return;
        }

        int id = hit.collider.gameObject.GetInstanceID();

        // 1) 배치된 미사일 고스트 클릭 → 미사일 선택
        if (ghostByInstanceId.TryGetValue(id, out PlacedMissile clickedMissile))
        {
            if (ctrl)
            {
                if (!selectedMissiles.Remove(clickedMissile))
                    selectedMissiles.Add(clickedMissile);
            }
            else
            {
                bool wasSelected = selectedMissiles.Contains(clickedMissile);
                ClearAllSelections();
                if (!wasSelected)
                    selectedMissiles.Add(clickedMissile);
            }
            SceneView.RepaintAll();
            return;
        }

        // 2) Place 모드 — 타일/스폰에 미사일 배치
        if (inPlaceMode && selectedPaletteIndex >= 0)
        {
            if (tileByInstanceId.TryGetValue(id, out Vector2Int coord))
            {
                PlaceMissile(paletteTypes[selectedPaletteIndex], $"T:{coord.x},{coord.y}",
                    tileWorldPositions[coord]);
                SceneView.RepaintAll();
                return;
            }
            if (spawnByInstanceId.TryGetValue(id, out string spId))
            {
                PlaceMissile(paletteTypes[selectedPaletteIndex], $"S:{spId}",
                    spawnWorldPositions[spId]);
                SceneView.RepaintAll();
                return;
            }
        }

        // 3) 통합 선택: Tile / SpawnPoint
        if (tileByInstanceId.TryGetValue(id, out Vector2Int tileCoord))
        {
            if (ctrl)
            {
                if (!SelectedTiles.Remove(tileCoord)) SelectedTiles.Add(tileCoord);
            }
            else if (SelectedTiles.Contains(tileCoord))
                SelectedTiles.Remove(tileCoord);
            else
            {
                ClearAllSelections();
                SelectedTiles.Add(tileCoord);
            }
        }
        else if (spawnByInstanceId.TryGetValue(id, out string spawnId))
        {
            if (ctrl)
            {
                if (!SelectedSpawnPoints.Remove(spawnId)) SelectedSpawnPoints.Add(spawnId);
            }
            else if (SelectedSpawnPoints.Contains(spawnId))
                SelectedSpawnPoints.Remove(spawnId);
            else
            {
                ClearAllSelections();
                SelectedSpawnPoints.Add(spawnId);
            }
        }

        SceneView.RepaintAll();
    }

    private static void ApplyBoxSelection(bool additive)
    {
        Rect screenBox = new Rect(
            Mathf.Min(dragStart.x, dragEnd.x),
            Mathf.Min(dragStart.y, dragEnd.y),
            Mathf.Abs(dragEnd.x - dragStart.x),
            Mathf.Abs(dragEnd.y - dragStart.y));

        if (!additive)
        {
            SelectedTiles.Clear();
            SelectedSpawnPoints.Clear();
        }

        // 타일 — 시각적 중심이 드래그 박스 안에 있으면 선택
        foreach (var kvp in tileWorldPositions)
        {
            Vector2 screenPoint = HandleUtility.WorldToGUIPoint(kvp.Value);
            if (screenBox.Contains(screenPoint))
                SelectedTiles.Add(kvp.Key);
        }

        // 스폰포인트
        foreach (var kvp in spawnWorldPositions)
        {
            Vector2 screenPoint = HandleUtility.WorldToGUIPoint(kvp.Value);
            if (screenBox.Contains(screenPoint))
                SelectedSpawnPoints.Add(kvp.Key);
        }

        SceneView.RepaintAll();
    }

    #endregion

    #region Drawing

    private static void DrawHoverHighlight()
    {
        // 타일 호버 — 반투명 채우기 + 노란 외곽선
        if (hoveredTile.HasValue &&
            tileWorldPositions.TryGetValue(hoveredTile.Value, out Vector3 tileCenter))
        {
            DrawTileRect(tileCenter, HoverFillColor, HoverColor);
        }

        // 스폰포인트 호버 — 노란 와이어 큐브
        if (hoveredSpawnId != null &&
            spawnWorldPositions.TryGetValue(hoveredSpawnId, out Vector3 spawnPos))
        {
            Color old = Handles.color;
            Handles.color = HoverColor;
            Handles.DrawWireCube(spawnPos, Vector3.one * 0.6f);
            Handles.color = old;
        }
    }

    private static void DrawSelectionMarkers()
    {
        if (SelectedTiles.Count == 0) return;

        foreach (Vector2Int t in SelectedTiles)
        {
            if (!tileWorldPositions.TryGetValue(t, out Vector3 tileCenter)) continue;

            // 반투명 채우기 + 파란 외곽선
            DrawTileRect(tileCenter, SelectionFillColor, SelectionColor);

            // 좌표 레이블
            Handles.Label(
                new Vector3(tileCenter.x, platformOrigin.y + 0.1f, tileCenter.z),
                $"({t.x},{t.y})",
                EditorStyles.centeredGreyMiniLabel);
        }
    }

    private static void DrawSpawnSelectionMarkers()
    {
        if (SelectedSpawnPoints.Count == 0) return;

        Color old = Handles.color;
        Handles.color = SpawnSelectColor;

        foreach (string spawnId in SelectedSpawnPoints)
        {
            if (!spawnWorldPositions.TryGetValue(spawnId, out Vector3 pos)) continue;
            Handles.DrawWireCube(pos, Vector3.one * 0.6f);
        }

        Handles.color = old;
    }

    private static void DrawTileRect(Vector3 center, Color fill, Color outline)
    {
        float hw = tileXSize * 0.5f;
        float hd = tileZSize * 0.5f;
        float y  = platformOrigin.y + 0.02f;

        var corners = new Vector3[]
        {
            new Vector3(center.x - hw, y, center.z + hd),
            new Vector3(center.x + hw, y, center.z + hd),
            new Vector3(center.x + hw, y, center.z - hd),
            new Vector3(center.x - hw, y, center.z - hd),
        };

        Handles.DrawSolidRectangleWithOutline(corners, fill, outline);
    }

    private static void DrawDragBox()
    {
        Handles.BeginGUI();

        Rect box = new Rect(
            Mathf.Min(dragStart.x, dragEnd.x),
            Mathf.Min(dragStart.y, dragEnd.y),
            Mathf.Abs(dragEnd.x - dragStart.x),
            Mathf.Abs(dragEnd.y - dragStart.y));

        // 반투명 채우기
        EditorGUI.DrawRect(box, DragBoxFillColor);

        // 외곽선
        Color old = Handles.color;
        Handles.color = DragBoxOutlineColor;
        Vector3 bl = new Vector3(box.xMin, box.yMin);
        Vector3 br = new Vector3(box.xMax, box.yMin);
        Vector3 tr = new Vector3(box.xMax, box.yMax);
        Vector3 tl = new Vector3(box.xMin, box.yMax);
        Handles.DrawLine(bl, br);
        Handles.DrawLine(br, tr);
        Handles.DrawLine(tr, tl);
        Handles.DrawLine(tl, bl);
        Handles.color = old;

        Handles.EndGUI();
    }

    private static void DrawPalette(SceneView sceneView)
    {
        if (palettePrefabs.Count == 0) return;

        Handles.BeginGUI();

        float totalH = PaletteHeaderH + PalettePad + palettePrefabs.Count * (PaletteItemH + PalettePad);
        Rect panelRect = new Rect(PaletteX, PaletteY, PaletteWidth, totalH);

        // 배경
        EditorGUI.DrawRect(panelRect, new Color(0.15f, 0.15f, 0.15f, 0.85f));

        // 헤더
        Rect headerRect = new Rect(panelRect.x + PalettePad, panelRect.y + PalettePad,
            panelRect.width - PalettePad * 2f, PaletteHeaderH);
        GUI.Label(headerRect, inPlaceMode ? "Palette (Place Mode)" : "Palette",
            EditorStyles.boldLabel);

        float y = headerRect.yMax + PalettePad;

        for (int i = 0; i < palettePrefabs.Count; i++)
        {
            Rect itemRect = new Rect(panelRect.x + PalettePad, y,
                panelRect.width - PalettePad * 2f, PaletteItemH);

            bool selected = selectedPaletteIndex == i;

            // 선택된 항목 하이라이트
            if (selected)
                EditorGUI.DrawRect(itemRect, new Color(0.25f, 0.55f, 1.00f, 0.35f));

            // 타입 태그 + 이름
            string typeName = paletteTypes[i] switch
            {
                PlacedMissileType.Grand => "[G]",
                PlacedMissileType.Hover => "[H]",
                _                      => "[F]",
            };
            string label = $"{typeName} {palettePrefabs[i].name}";

            if (GUI.Button(itemRect, label, EditorStyles.label))
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
                    inPlaceMode          = true;
                }
                sceneView.Repaint();
            }

            y += PaletteItemH + PalettePad;
        }

        Handles.EndGUI();
    }

    #endregion

    #region Missile Placement

    private static void PlaceMissile(PlacedMissileType type, string locationKey, Vector3 worldPos)
    {
        // 팔레트에서 해당 타입의 프리팹 찾기
        int prefabIndex = selectedPaletteIndex;
        if (prefabIndex < 0 || prefabIndex >= palettePrefabs.Count) return;

        bool onSpawnPoint = locationKey.StartsWith("S:");
        string spawnId = onSpawnPoint ? locationKey.Substring(2) : null;

        // 실제 게임 스폰 높이 적용
        Vector3 spawnPos = CalculateSpawnPosition(type, worldPos, onSpawnPoint, spawnId);

        var ghost = InstantiateMissilePrefab(palettePrefabs[prefabIndex], spawnPos, type,
            onSpawnPoint, spawnId);
        if (ghost == null) return;

        var placed = new PlacedMissile
        {
            type        = type,
            locationKey = locationKey,
            ghost       = ghost,
        };
        placedMissiles.Add(placed);

        // 고스트의 모든 Collider instanceId를 등록 (레이캐스트 히트용)
        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            ghostByInstanceId[col.gameObject.GetInstanceID()] = placed;
    }

    /// <summary>
    /// 스폰포인트 ID에서 플랫폼을 향하는 방향 벡터를 반환.
    /// N→남쪽, S→북쪽, E→서쪽, W→동쪽, 대각선은 해당 대각 방향.
    /// MissileSpawner의 HorizonLinear 방향과 동일.
    /// </summary>
    private static Vector3 GetSpawnFacingDirection(string spawnId)
    {
        if (spawnId.StartsWith("N:"))  return Vector3.back;     // N → -Z (남쪽)
        if (spawnId.StartsWith("S:"))  return Vector3.forward;  // S → +Z (북쪽)
        if (spawnId.StartsWith("E:"))  return Vector3.left;     // E → -X (서쪽)
        if (spawnId.StartsWith("W:"))  return Vector3.right;    // W → +X (동쪽)
        if (spawnId == "NE") return new Vector3(-1f, 0f, -1f).normalized;
        if (spawnId == "NW") return new Vector3( 1f, 0f, -1f).normalized;
        if (spawnId == "SE") return new Vector3(-1f, 0f,  1f).normalized;
        if (spawnId == "SW") return new Vector3( 1f, 0f,  1f).normalized;
        return Vector3.zero;
    }

    private static Vector3 CalculateSpawnPosition(PlacedMissileType type, Vector3 worldPos,
        bool onSpawnPoint = false, string spawnId = null)
    {
        switch (type)
        {
            case PlacedMissileType.Falling:
                // Falling: 타일 중심 XZ + MissileDropPoint 높이
                return new Vector3(worldPos.x, platformOrigin.y + FallingSpawnHeight, worldPos.z);

            case PlacedMissileType.Grand:
                if (onSpawnPoint)
                {
                    // Grand Horizontal: 스폰포인트 위치 + diameter 기반 Y 오프셋 (기본 diameter 3 = +5.5)
                    return new Vector3(worldPos.x, worldPos.y + GrandHorizontalYOffset, worldPos.z);
                }
                // Grand Vertical: 타일 중심 XZ + MissileDropPoint 높이
                return new Vector3(worldPos.x, platformOrigin.y + FallingSpawnHeight, worldPos.z);

            case PlacedMissileType.Hover:
                // Hover: 스폰포인트 위치 + 2.5 Y 오프셋
                return new Vector3(worldPos.x, worldPos.y + HoverSpawnYOffset, worldPos.z);

            default:
                return new Vector3(worldPos.x, platformOrigin.y + 0.5f, worldPos.z);
        }
    }

    private static GameObject InstantiateMissilePrefab(GameObject prefab, Vector3 worldPos,
        PlacedMissileType type = PlacedMissileType.Falling,
        bool onSpawnPoint = false, string spawnId = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (go == null) return null;

        go.transform.SetParent(rootObject.transform);
        go.transform.position = worldPos;
        go.hideFlags = HideFlags.DontSave;

        // 런타임 컴포넌트 비활성화 — 에디터에서 물리/스크립트 동작 방지
        DisableRuntimeComponents(go);

        // 타입별 회전 적용 (GrandMissile.Initialize 게임 코드와 동일)
        ApplyMissileRotation(go, type, onSpawnPoint, spawnId);

        // 데칼을 플랫폼 표면에 배치 (게임 런타임에서는 스크립트가 하지만, Edit 모드에서는 수동)
        SetupDecals(go, type, onSpawnPoint, spawnId);

        return go;
    }

    /// <summary>
    /// GrandMissile.Initialize()의 회전 로직을 에디터에서 재현.
    /// direction 0=Vertical, 1=N→S, 2=S→N, 3=E→W, 4=W→E
    /// </summary>
    private static void ApplyMissileRotation(GameObject go, PlacedMissileType type,
        bool onSpawnPoint, string spawnId)
    {
        switch (type)
        {
            case PlacedMissileType.Grand:
                if (!onSpawnPoint)
                {
                    // Grand Vertical (direction 0): 아래로 낙하
                    go.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
                }
                else
                {
                    // Grand Horizontal: 스폰 방향에 따른 회전
                    int dir = GetGrandDirection(spawnId);
                    go.transform.rotation = dir switch
                    {
                        1 => Quaternion.Euler(-90f, 0f, 0f),   // N→S
                        2 => Quaternion.Euler(90f, 0f, 0f),    // S→N
                        3 => Quaternion.Euler(0f, 0f, 90f),    // E→W
                        4 => Quaternion.Euler(0f, 0f, -90f),   // W→E
                        _ => Quaternion.Euler(180f, 0f, 0f),
                    };
                }
                break;

            case PlacedMissileType.Hover:
                if (onSpawnPoint && spawnId != null)
                {
                    Vector3 facing = GetSpawnFacingDirection(spawnId);
                    if (facing != Vector3.zero)
                        go.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
                }
                break;

            // Falling: 프리팹 기본 회전(180,0,0) 유지
        }
    }

    /// <summary>
    /// 스폰포인트 ID → GrandMissile direction 값 (1=N→S, 2=S→N, 3=E→W, 4=W→E)
    /// </summary>
    private static int GetGrandDirection(string spawnId)
    {
        if (spawnId == null) return 0;
        if (spawnId.StartsWith("N:")) return 1;  // N→S
        if (spawnId.StartsWith("S:")) return 2;  // S→N
        if (spawnId.StartsWith("E:")) return 3;  // E→W
        if (spawnId.StartsWith("W:")) return 4;  // W→E
        return 0; // 대각선은 일단 Vertical 취급
    }

    private static void DisableRuntimeComponents(GameObject go)
    {
        // Rigidbody 제거
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(rb);

        // Missile 스크립트 비활성화 (데칼은 유지 — 시각적으로 필요)
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var typeName = mb.GetType().Name;
            if (typeName.Contains("Missile") || typeName.Contains("Particle"))
                mb.enabled = false;
        }

        // 파티클 시스템 중지
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void SetupDecals(GameObject go, PlacedMissileType type,
        bool onSpawnPoint = false, string spawnId = null)
    {
        // warningDecal 자식 오브젝트를 찾아서 플랫폼 표면에 배치
        // 게임 런타임에서는 FallingMissile.FixedUpdate / GrandMissile.FixedUpdate에서 처리하지만
        // Edit 모드에서는 스크립트가 동작하지 않으므로 수동 배치

        Vector3 missilePos = go.transform.position;

        switch (type)
        {
            case PlacedMissileType.Falling:
            {
                var decalGO = go.transform.Find("RangeDecal");
                if (decalGO != null)
                {
                    decalGO.gameObject.SetActive(true);
                    decalGO.position = new Vector3(
                        missilePos.x,
                        platformOrigin.y + FallingDecalYOffset,
                        missilePos.z);
                    decalGO.rotation = Quaternion.Euler(90f, 0f, 0f);
                    SetDecalProjector(decalGO.gameObject,
                        new Vector3(tileXSize, tileZSize, 0.5f),
                        Vector3.zero);
                }
                break;
            }
            case PlacedMissileType.Grand:
            {
                var vertDecal  = go.transform.Find("GrandVerticalDecal");
                var horizDecal = go.transform.Find("GrandHorizenDecal");

                if (onSpawnPoint)
                {
                    // Grand Horizontal: 수평 데칼 활성, 수직 데칼 비활성
                    if (vertDecal != null) vertDecal.gameObject.SetActive(false);
                    if (horizDecal != null)
                    {
                        horizDecal.gameObject.SetActive(true);
                        SetupGrandHorizontalDecal(horizDecal, missilePos, spawnId);
                    }
                }
                else
                {
                    // Grand Vertical: 수직 데칼을 바닥에 표시, 수평 데칼 비활성
                    if (vertDecal != null)
                    {
                        vertDecal.gameObject.SetActive(true);
                        vertDecal.position = new Vector3(
                            missilePos.x,
                            platformOrigin.y + GrandDecalYOffset,
                            missilePos.z);
                        vertDecal.rotation = Quaternion.Euler(90f, 0f, 0f);

                        // 게임: desiredSize = tileScale.x * diameter, pivot = (0, 0, -1.5)
                        float desiredSize = tileXSize * DefaultGrandDiameter;
                        SetDecalProjector(vertDecal.gameObject,
                            new Vector3(desiredSize, desiredSize, 0.5f),
                            new Vector3(0f, 0f, -1.5f));
                    }
                    if (horizDecal != null) horizDecal.gameObject.SetActive(false);
                }
                break;
            }
            // Hover: 데칼 없음
        }
    }

    private static void SetupGrandHorizontalDecal(Transform decalTransform,
        Vector3 missilePos, string spawnId)
    {
        // GrandMissile.Start() 게임 코드 재현 — 방향별 회전, 피벗, 크기 설정
        int dir = GetGrandDirection(spawnId);

        // 게임 코드 GrandMissile.Start() 재현
        // 데칼은 미사일 자식(localPosition 0,0,0)에 두고 pivot/rotation으로 프로젝션 제어
        // 미사일 부모가 이미 방향별 회전이 적용되어 있으므로 localRotation 사용

        Vector3 pivot = new Vector3(15f, 0f, 1f);

        switch (dir)
        {
            case 1: // N→S
                decalTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                pivot.z = -1f;
                break;
            case 2: // S→N
                decalTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                break;
            case 3: // E→W
                decalTransform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                pivot.z = -1f;
                break;
            case 4: // W→E
                decalTransform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                break;
            default:
                return;
        }

        // diameter 3 기준: fixPivot.z *= 4.5, decalSize.y = 6
        // 프리팹 초기 size = (30, 10, 0.5), 게임 코드에서 y만 diameter 기준으로 변경
        pivot.z *= 4.51f;

        SetDecalProjector(decalTransform.gameObject,
            new Vector3(30f, 6f, 0.5f),
            pivot);
    }

    private static void SetDecalProjector(GameObject decalGO, Vector3 size, Vector3 pivot)
    {
        var projector = decalGO.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
        if (projector == null) return;

        projector.enabled = true;
        projector.size  = size;
        projector.pivot = pivot;
    }


    private static void RemoveSelectedMissiles()
    {
        foreach (var m in selectedMissiles)
        {
            if (m.ghost != null)
            {
                foreach (var col in m.ghost.GetComponentsInChildren<Collider>())
                    ghostByInstanceId.Remove(col.gameObject.GetInstanceID());
                Object.DestroyImmediate(m.ghost);
            }
            placedMissiles.Remove(m);
        }
        selectedMissiles.Clear();
    }

    private static void ClearAllPlacedMissiles()
    {
        foreach (var m in placedMissiles)
        {
            if (m.ghost != null)
                Object.DestroyImmediate(m.ghost);
        }
        placedMissiles.Clear();
        ghostByInstanceId.Clear();
        selectedMissiles.Clear();
    }

    private static void ClearAllSelections()
    {
        SelectedTiles.Clear();
        SelectedSpawnPoints.Clear();
        selectedMissiles.Clear();
    }

    #endregion

    #region Missile Drawing

    private static void DrawMissileSelectionMarkers()
    {
        if (selectedMissiles.Count == 0) return;

        Color old = Handles.color;
        Handles.color = MissileSelectColor;

        foreach (var m in selectedMissiles)
        {
            if (m.ghost == null) continue;
            var bounds = m.ghost.GetComponent<Renderer>().bounds;
            Handles.DrawWireCube(bounds.center, bounds.size * 1.2f);
        }

        Handles.color = old;
    }

    private static void UpdateHoverGhost()
    {
        if (!inPlaceMode || selectedPaletteIndex < 0)
        {
            DestroyHoverGhost();
            return;
        }

        // 현재 호버 위치 결정
        string newLocation = null;
        Vector3 ghostPos = Vector3.zero;

        if (hoveredTile.HasValue && tileWorldPositions.TryGetValue(hoveredTile.Value, out Vector3 tPos))
        {
            newLocation = $"T:{hoveredTile.Value.x},{hoveredTile.Value.y}";
            ghostPos = tPos;
        }
        else if (hoveredSpawnId != null && spawnWorldPositions.TryGetValue(hoveredSpawnId, out Vector3 sPos))
        {
            newLocation = $"S:{hoveredSpawnId}";
            ghostPos = sPos;
        }

        if (newLocation == null)
        {
            DestroyHoverGhost();
            return;
        }

        // 위치가 바뀌었으면 고스트 재생성
        if (hoverGhostLocation != newLocation)
        {
            DestroyHoverGhost();

            var type = paletteTypes[selectedPaletteIndex];
            bool onSpawn = hoveredSpawnId != null;
            Vector3 spawnPos = CalculateSpawnPosition(type, ghostPos, onSpawn, hoveredSpawnId);
            hoverGhost = InstantiateMissilePrefab(palettePrefabs[selectedPaletteIndex], spawnPos,
                type, onSpawn, hoveredSpawnId);
            hoverGhostLocation = newLocation;

            if (hoverGhost != null)
            {
                // 레이캐스트에 걸리지 않도록 Collider 전부 비활성화
                foreach (var col in hoverGhost.GetComponentsInChildren<Collider>(true))
                    col.enabled = false;
            }
        }
    }

    private static void DestroyHoverGhost()
    {
        if (hoverGhost != null)
            Object.DestroyImmediate(hoverGhost);
        hoverGhost         = null;
        hoverGhostLocation = null;
    }

    #endregion
}
