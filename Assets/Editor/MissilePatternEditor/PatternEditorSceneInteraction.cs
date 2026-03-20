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
    private static readonly Color GrandHStripFill     = new Color(1.00f, 0.65f, 0.10f, 0.18f);
    private static readonly Color GrandHStripOutline  = new Color(1.00f, 0.65f, 0.10f, 0.70f);

    // 미사일 타입별 색상 (겹침 리스트 스와치용)
    private static readonly Color MissileFallingColor = new Color(0.30f, 0.75f, 1.00f);
    private static readonly Color MissileGrandColor   = new Color(1.00f, 0.35f, 0.25f);
    private static readonly Color MissileGrandHColor  = new Color(1.00f, 0.75f, 0.20f);
    private static readonly Color MissileHoverColor   = new Color(0.50f, 1.00f, 0.50f);

    #endregion

    #region State

    private static bool initialized;
    private static bool interactionEnabled = true;

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
    private static int        hoverGhostPaletteIndex = -1;
    private static string     hoverGhostRotationKey;  // 회전 컨텍스트 — 변경 시 재생성 필요

    private static readonly Color MissileSelectColor = new Color(1f, 1f, 0f, 0.90f);

    // Undo/Redo 선택 스택
    private static readonly Stack<(HashSet<Vector2Int> tiles, HashSet<string> spawns)> undoStack
        = new Stack<(HashSet<Vector2Int>, HashSet<string>)>();
    private static readonly Stack<(HashSet<Vector2Int> tiles, HashSet<string> spawns)> redoStack
        = new Stack<(HashSet<Vector2Int>, HashSet<string>)>();

    // 겹침 순환 선택
    private static readonly List<PlacedMissile> overlapList = new List<PlacedMissile>();

    #endregion

    #region Public API

    public static bool Initialized => initialized;
    public static bool InteractionEnabled => interactionEnabled;

    /// <summary>
    /// 에디터 모드 토글 — false 시 입력 처리를 건너뛰고 시각화만 유지.
    /// </summary>
    public static void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        SceneView.RepaintAll();
    }

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

        undoStack.Clear();
        redoStack.Clear();
        overlapList.Clear();

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

        // 시각화는 항상 렌더링 (기본 도구 모드에서도 배치된 미사일·선택 마커 보임)
        DrawSelectionMarkers();
        DrawSpawnSelectionMarkers();
        DrawMissileSelectionMarkers();
        DrawGrandHorizontalStrips();

        // 에디터 모드 비활성 시 입력·호버·팔레트 건너뜀
        if (!interactionEnabled) return;

        Event e = Event.current;

        HandleInput(e);
        DrawHoverHighlight();
        UpdateHoverGhost();
        DrawPalette(sceneView);
        DrawOverlapPanel(sceneView);

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

    private static bool IsMouseOverOverlapPanel(Vector2 mousePos)
    {
        if (overlapList.Count <= 1) return false;
        const float ItemH   = 20f;
        const float ListW   = 140f;
        const float Margin  = 8f;
        const float HeaderH = 18f;
        float paletteH = PaletteHeaderH + PalettePad + palettePrefabs.Count * (PaletteItemH + PalettePad);
        float listX = PaletteX;
        float listY = PaletteY + paletteH + Margin;
        float contentH = overlapList.Count * ItemH + 4f + HeaderH;
        // 실제 표시 높이는 뷰포트에 맞춰 제한됨
        float panelW = ListW + 14f; // 스크롤바 최대 폭 포함
        return new Rect(listX, listY, panelW, contentH).Contains(mousePos);
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

        // 팔레트·겹침패널 영역 내 클릭은 월드 인터랙션 무시 (GUI가 처리)
        if (e.isMouse && (IsMouseOverPalette(e.mousePosition) || IsMouseOverOverlapPanel(e.mousePosition)))
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

        // Delete 키 — 선택된 미사일 삭제, 없으면 선택된 타일/스폰 위의 미사일 삭제
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
        {
            DeleteSelectedObjects();
            SceneView.RepaintAll();
            e.Use();
            return;
        }

        // Escape 키 — 캐스케이드 해제: Brush → 미사일 선택 → 타일/스폰 선택
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            if (inPlaceMode)
            {
                selectedPaletteIndex = -1;
                inPlaceMode          = false;
            }
            else if (selectedMissiles.Count > 0)
            {
                selectedMissiles.Clear();
                overlapList.Clear();
            }
            else if (SelectedTiles.Count > 0 || SelectedSpawnPoints.Count > 0)
            {
                PushUndo();
                SelectedTiles.Clear();
                SelectedSpawnPoints.Clear();
            }
            SceneView.RepaintAll();
            e.Use();
            return;
        }

        // Ctrl+Z / Ctrl+Y — Undo / Redo 선택 상태
        if (e.type == EventType.KeyDown && e.control)
        {
            if (e.keyCode == KeyCode.Z)
            {
                UndoSelection();
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.Y)
            {
                RedoSelection();
                e.Use();
                return;
            }
        }

        // 우클릭 — Place 모드 해제, 아니면 커서 아래 객체 즉시 삭제
        if (e.type == EventType.MouseDown && e.button == 1 && !e.alt)
        {
            if (inPlaceMode)
            {
                selectedPaletteIndex = -1;
                inPlaceMode          = false;
                SceneView.RepaintAll();
                e.Use();
            }
            else if (ApplyRightClickDelete())
            {
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
        // 1) 배치된 미사일 고스트 클릭 → 미사일 선택 (겹침 순환)
        if (ghostByInstanceId.TryGetValue(id, out PlacedMissile clickedMissile))
        {
            var hits = GetAllMissilesAtLocation(clickedMissile.locationKey);
            HandleMissileSelectionCycle(hits, ctrl);
            return;
        }

        // 2) Place 모드 — 타일/스폰에 미사일 배치
        if (inPlaceMode && selectedPaletteIndex >= 0)
        {
            if (tileByInstanceId.TryGetValue(id, out Vector2Int coord))
            {
                string locKey = $"T:{coord.x},{coord.y}";
                PlaceMissile(paletteTypes[selectedPaletteIndex], locKey,
                    tileWorldPositions[coord]);
                // 배치 후 겹침 리스트 갱신
                overlapList.Clear();
                overlapList.AddRange(GetAllMissilesAtLocation(locKey));
                SceneView.RepaintAll();
                return;
            }
            if (spawnByInstanceId.TryGetValue(id, out string spId))
            {
                string locKey = $"S:{spId}";
                PlaceMissile(paletteTypes[selectedPaletteIndex], locKey,
                    spawnWorldPositions[spId]);
                // 배치 후 겹침 리스트 갱신
                overlapList.Clear();
                overlapList.AddRange(GetAllMissilesAtLocation(locKey));
                SceneView.RepaintAll();
                return;
            }
        }

        // 3) 통합 선택: Tile / SpawnPoint (미사일이 배치된 위치면 미사일 선택 우선)
        if (tileByInstanceId.TryGetValue(id, out Vector2Int tileCoord))
        {
            string locKey = $"T:{tileCoord.x},{tileCoord.y}";
            var missilesHere = GetAllMissilesAtLocation(locKey);

            if (missilesHere.Count > 0 && !inPlaceMode)
            {
                // 미사일이 있는 타일 클릭 → 미사일 선택/순환
                HandleMissileSelectionCycle(missilesHere, ctrl);
            }
            else if (ctrl)
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
            string locKey = $"S:{spawnId}";
            var missilesHere = GetAllMissilesAtLocation(locKey);

            if (missilesHere.Count > 0 && !inPlaceMode)
            {
                // 미사일이 있는 스폰 클릭 → 미사일 선택/순환
                HandleMissileSelectionCycle(missilesHere, ctrl);
            }
            else if (ctrl)
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
        PushUndo();

        Rect screenBox = new Rect(
            Mathf.Min(dragStart.x, dragEnd.x),
            Mathf.Min(dragStart.y, dragEnd.y),
            Mathf.Abs(dragEnd.x - dragStart.x),
            Mathf.Abs(dragEnd.y - dragStart.y));

        if (!additive)
        {
            SelectedTiles.Clear();
            SelectedSpawnPoints.Clear();
            selectedMissiles.Clear();
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

        // 미사일 — 고스트 중심이 드래그 박스 안에 있으면 선택
        foreach (var m in placedMissiles)
        {
            if (m.ghost == null) continue;
            var renderer = m.ghost.GetComponentInChildren<Renderer>();
            if (renderer == null) continue;
            Vector2 screenPoint = HandleUtility.WorldToGUIPoint(renderer.bounds.center);
            if (screenBox.Contains(screenPoint))
                selectedMissiles.Add(m);
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

    /// <summary>
    /// 배치된 Grand Horizontal 미사일의 경로 스트립을 Handles로 시각화.
    /// DecalProjector ShaderGraph가 Edit 모드에서 무거우므로 경량 대체 시각화.
    /// </summary>
    private static void DrawGrandHorizontalStrips()
    {
        float y         = platformOrigin.y + 0.03f;
        float halfWidth = tileXSize * DefaultGrandDiameter * 0.5f;

        // tileWorldPositions에서 정확한 플랫폼 경계 계산
        var topLeft     = new Vector2Int(0, 0);
        var topRight    = new Vector2Int(GridCols - 1, 0);
        var bottomLeft  = new Vector2Int(0, GridRows - 1);
        if (!tileWorldPositions.ContainsKey(topLeft) || !tileWorldPositions.ContainsKey(bottomLeft)) return;

        float platMinX = tileWorldPositions[topLeft].x    - tileXSize * 0.5f;
        float platMaxX = tileWorldPositions[topRight].x   + tileXSize * 0.5f;
        float platMaxZ = tileWorldPositions[topLeft].z     + tileZSize * 0.5f;
        float platMinZ = tileWorldPositions[bottomLeft].z  - tileZSize * 0.5f;

        foreach (var m in placedMissiles)
        {
            if (m.type != PlacedMissileType.Grand || m.ghost == null) continue;
            if (!m.locationKey.StartsWith("S:")) continue;

            string spawnId = m.locationKey.Substring(2);
            int dir = GetGrandDirection(spawnId);
            if (dir == 0) continue;

            Vector3 pos = m.ghost.transform.position;
            Vector3[] corners;

            if (dir <= 2) // N→S / S→N: Z축 방향 스트립
            {
                float left  = Mathf.Max(pos.x - halfWidth, platMinX);
                float right = Mathf.Min(pos.x + halfWidth, platMaxX);
                corners = new Vector3[]
                {
                    new Vector3(left,  y, platMaxZ),
                    new Vector3(right, y, platMaxZ),
                    new Vector3(right, y, platMinZ),
                    new Vector3(left,  y, platMinZ),
                };
            }
            else // E→W / W→E: X축 방향 스트립
            {
                float top    = Mathf.Min(pos.z + halfWidth, platMaxZ);
                float bottom = Mathf.Max(pos.z - halfWidth, platMinZ);
                corners = new Vector3[]
                {
                    new Vector3(platMinX, y, top),
                    new Vector3(platMaxX, y, top),
                    new Vector3(platMaxX, y, bottom),
                    new Vector3(platMinX, y, bottom),
                };
            }

            Handles.DrawSolidRectangleWithOutline(corners, GrandHStripFill, GrandHStripOutline);
        }
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
                    var type = paletteTypes[i];
                    bool isHover = type == PlacedMissileType.Hover;
                    bool isGrand = type == PlacedMissileType.Grand;
                    bool hasValidSelection = isHover
                        ? SelectedSpawnPoints.Count > 0
                        : isGrand
                            ? SelectedTiles.Count > 0 || SelectedSpawnPoints.Count > 0
                            : SelectedTiles.Count > 0;

                    if (hasValidSelection)
                    {
                        // Select→Action: 선택된 위치에 즉시 다중 배치
                        if (isHover)
                        {
                            foreach (string spId in SelectedSpawnPoints)
                                PlaceMissile(type, $"S:{spId}", spawnWorldPositions[spId]);
                        }
                        else if (isGrand)
                        {
                            foreach (var tile in SelectedTiles)
                                PlaceMissile(type, $"T:{tile.x},{tile.y}", tileWorldPositions[tile]);
                            foreach (string spId in SelectedSpawnPoints)
                                PlaceMissile(type, $"S:{spId}", spawnWorldPositions[spId]);
                        }
                        else
                        {
                            foreach (var tile in SelectedTiles)
                                PlaceMissile(type, $"T:{tile.x},{tile.y}", tileWorldPositions[tile]);
                        }
                        // 배치 후 Place 모드 진입하지 않음
                        selectedPaletteIndex = -1;
                        inPlaceMode          = false;
                    }
                    else
                    {
                        inPlaceMode = true;
                    }
                }
                sceneView.Repaint();
            }

            y += PaletteItemH + PalettePad;
        }

        Handles.EndGUI();
    }

    private static Vector2 overlapScrollPos;

    private static void DrawOverlapPanel(SceneView sceneView)
    {
        // 유효하지 않은 항목 제거
        overlapList.RemoveAll(m => m.ghost == null);
        if (overlapList.Count <= 1) return;

        Handles.BeginGUI();

        const float ItemH   = 20f;
        const float ListW   = 140f;
        const float SwatchW =  8f;
        const float Margin  =  8f;
        const float HeaderH = 18f;

        // 팔레트 패널 아래에 배치
        float paletteH = PaletteHeaderH + PalettePad + palettePrefabs.Count * (PaletteItemH + PalettePad);
        float listX = PaletteX;
        float listY = PaletteY + paletteH + Margin;

        float contentH = overlapList.Count * ItemH + 4f;
        float availableH = sceneView.position.height - listY - Margin;
        float maxPanelH = availableH > 0 ? availableH : 200f;
        float panelH = Mathf.Min(contentH + HeaderH, maxPanelH);
        bool needsScroll = contentH + HeaderH > panelH;
        // 스크롤바 폭 보정
        float scrollBarW = needsScroll ? 14f : 0f;

        Rect panelRect = new Rect(listX, listY, ListW + scrollBarW, panelH);

        // 배경
        EditorGUI.DrawRect(panelRect, new Color(0f, 0f, 0f, 0.72f));

        // 헤더
        GUI.Label(new Rect(listX + 4f, listY + 1f, ListW - 8f, HeaderH),
            $"Overlap ({overlapList.Count})", EditorStyles.miniBoldLabel);

        // 스크롤 영역
        Rect viewRect    = new Rect(listX, listY + HeaderH, ListW + scrollBarW, panelH - HeaderH);
        Rect contentRect = new Rect(0f, 0f, ListW, contentH);

        overlapScrollPos = GUI.BeginScrollView(viewRect, overlapScrollPos, contentRect);

        Event e = Event.current;
        for (int i = 0; i < overlapList.Count; i++)
        {
            var m = overlapList[i];
            if (m.ghost == null) continue;

            var rowRect = new Rect(0f, 2f + i * ItemH, ListW, ItemH);

            // 선택된 항목 배경 하이라이트
            if (selectedMissiles.Contains(m))
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.15f));

            // 클릭 → 해당 미사일 선택
            if (e.type == EventType.MouseDown && e.button == 0 && rowRect.Contains(e.mousePosition))
            {
                selectedMissiles.Clear();
                selectedMissiles.Add(m);
                e.Use();
                sceneView.Repaint();
            }

            // 타입별 색상 스와치
            bool isGrandH = m.type == PlacedMissileType.Grand && m.locationKey.StartsWith("S:");
            Color typeColor = m.type switch
            {
                PlacedMissileType.Falling => MissileFallingColor,
                PlacedMissileType.Grand   => isGrandH ? MissileGrandHColor : MissileGrandColor,
                PlacedMissileType.Hover   => MissileHoverColor,
                _                         => Color.white
            };
            EditorGUI.DrawRect(new Rect(4f, rowRect.y + 6f, SwatchW, 8f), typeColor);

            // 이름 레이블
            GUI.Label(new Rect(4f + SwatchW + 4f, rowRect.y, ListW - SwatchW - 12f, ItemH),
                $"{m.type} {i + 1}", EditorStyles.miniLabel);
        }

        GUI.EndScrollView();
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

        // Edit 모드에서 ShaderGraph 애니메이션 데칼 비활성 — Handles 시각화로 대체
        var projector = decalTransform.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
        if (projector != null)
            projector.enabled = false;
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
            RemoveSingleMissile(m);
        selectedMissiles.Clear();
        overlapList.RemoveAll(m => m.ghost == null);
    }

    private static void RemoveSingleMissile(PlacedMissile m)
    {
        if (m.ghost != null)
        {
            foreach (var col in m.ghost.GetComponentsInChildren<Collider>())
                ghostByInstanceId.Remove(col.gameObject.GetInstanceID());
            Object.DestroyImmediate(m.ghost);
        }
        placedMissiles.Remove(m);
    }

    /// <summary>
    /// Delete 키: 선택된 미사일 삭제. 미사일 없으면 선택된 타일/스폰 위의 미사일 삭제.
    /// </summary>
    private static void DeleteSelectedObjects()
    {
        if (selectedMissiles.Count > 0)
        {
            RemoveSelectedMissiles();
            return;
        }

        bool changed = false;
        foreach (var tile in SelectedTiles)
        {
            for (int i = placedMissiles.Count - 1; i >= 0; i--)
            {
                var m = placedMissiles[i];
                if (m.locationKey == $"T:{tile.x},{tile.y}")
                {
                    RemoveSingleMissile(m);
                    changed = true;
                }
            }
        }
        foreach (string spawnId in SelectedSpawnPoints)
        {
            for (int i = placedMissiles.Count - 1; i >= 0; i--)
            {
                var m = placedMissiles[i];
                if (m.locationKey == $"S:{spawnId}")
                {
                    RemoveSingleMissile(m);
                    changed = true;
                }
            }
        }
        if (changed)
            overlapList.RemoveAll(m => m.ghost == null);
    }

    /// <summary>
    /// 우클릭: 커서 아래 객체 즉시 삭제. 우선순위 Missile > SpawnPoint 위 미사일 > Tile 위 미사일.
    /// </summary>
    private static bool ApplyRightClickDelete()
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return false;

        int id = hit.collider.gameObject.GetInstanceID();

        // 미사일 고스트 직접 클릭
        if (ghostByInstanceId.TryGetValue(id, out PlacedMissile clickedMissile))
        {
            selectedMissiles.Remove(clickedMissile);
            RemoveSingleMissile(clickedMissile);
            overlapList.RemoveAll(m => m.ghost == null);
            return true;
        }

        // 타일 위의 미사일 삭제
        if (tileByInstanceId.TryGetValue(id, out Vector2Int coord))
        {
            string locKey = $"T:{coord.x},{coord.y}";
            for (int i = placedMissiles.Count - 1; i >= 0; i--)
            {
                if (placedMissiles[i].locationKey == locKey)
                {
                    selectedMissiles.Remove(placedMissiles[i]);
                    RemoveSingleMissile(placedMissiles[i]);
                }
            }
            overlapList.RemoveAll(m => m.ghost == null);
            return true;
        }

        // 스폰포인트 위의 미사일 삭제
        if (spawnByInstanceId.TryGetValue(id, out string spawnId))
        {
            string locKey = $"S:{spawnId}";
            for (int i = placedMissiles.Count - 1; i >= 0; i--)
            {
                if (placedMissiles[i].locationKey == locKey)
                {
                    selectedMissiles.Remove(placedMissiles[i]);
                    RemoveSingleMissile(placedMissiles[i]);
                }
            }
            overlapList.RemoveAll(m => m.ghost == null);
            return true;
        }

        return false;
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

    private static void HandleMissileSelectionCycle(List<PlacedMissile> hits, bool ctrl)
    {
        if (ctrl)
        {
            // Ctrl+클릭: 첫 번째 미사일 토글
            if (!selectedMissiles.Remove(hits[0]))
                selectedMissiles.Add(hits[0]);
        }
        else if (hits.Count == 1)
        {
            bool wasSelected = selectedMissiles.Contains(hits[0]);
            ClearAllSelections();
            if (!wasSelected) selectedMissiles.Add(hits[0]);
        }
        else
        {
            // 겹침 순환
            int currentIndex = -1;
            if (selectedMissiles.Count == 1)
            {
                PlacedMissile sel = null;
                foreach (var m in selectedMissiles) sel = m;
                currentIndex = hits.IndexOf(sel);
            }
            int nextIndex = (currentIndex + 1) % hits.Count;
            ClearAllSelections();
            selectedMissiles.Add(hits[nextIndex]);
        }
        overlapList.Clear();
        overlapList.AddRange(hits);
        SceneView.RepaintAll();
    }

    /// <summary>
    /// 같은 locationKey를 가진 모든 배치된 미사일을 반환 (겹침 순환 선택용).
    /// </summary>
    private static List<PlacedMissile> GetAllMissilesAtLocation(string locationKey)
    {
        var result = new List<PlacedMissile>();
        foreach (var m in placedMissiles)
            if (m.locationKey == locationKey)
                result.Add(m);
        return result;
    }

    #endregion

    #region Undo / Redo

    private static void PushUndo()
    {
        undoStack.Push((new HashSet<Vector2Int>(SelectedTiles), new HashSet<string>(SelectedSpawnPoints)));
        redoStack.Clear();
    }

    private static void UndoSelection()
    {
        if (undoStack.Count == 0) return;
        redoStack.Push((new HashSet<Vector2Int>(SelectedTiles), new HashSet<string>(SelectedSpawnPoints)));
        RestoreSelection(undoStack.Pop());
    }

    private static void RedoSelection()
    {
        if (redoStack.Count == 0) return;
        undoStack.Push((new HashSet<Vector2Int>(SelectedTiles), new HashSet<string>(SelectedSpawnPoints)));
        RestoreSelection(redoStack.Pop());
    }

    private static void RestoreSelection((HashSet<Vector2Int> tiles, HashSet<string> spawns) state)
    {
        SelectedTiles.Clear();
        foreach (var t in state.tiles) SelectedTiles.Add(t);
        SelectedSpawnPoints.Clear();
        foreach (var s in state.spawns) SelectedSpawnPoints.Add(s);
        SceneView.RepaintAll();
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
            var renderer = m.ghost.GetComponentInChildren<Renderer>();
            if (renderer == null) continue;
            var bounds = renderer.bounds;
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
        bool onSpawn = false;

        if (hoveredTile.HasValue && tileWorldPositions.TryGetValue(hoveredTile.Value, out Vector3 tPos))
        {
            newLocation = $"T:{hoveredTile.Value.x},{hoveredTile.Value.y}";
            ghostPos = tPos;
        }
        else if (hoveredSpawnId != null && spawnWorldPositions.TryGetValue(hoveredSpawnId, out Vector3 sPos))
        {
            newLocation = $"S:{hoveredSpawnId}";
            ghostPos = sPos;
            onSpawn = true;
        }

        if (newLocation == null)
        {
            DestroyHoverGhost();
            return;
        }

        if (hoverGhostLocation == newLocation) return;

        var type = paletteTypes[selectedPaletteIndex];

        // 회전 컨텍스트: 팔레트 + 위치 종류(타일/스폰방향)에 따라 결정
        // 같으면 위치만 이동, 다르면 재생성
        string rotationKey = BuildRotationKey(selectedPaletteIndex, onSpawn, hoveredSpawnId);
        bool canReuse = hoverGhost != null
                     && hoverGhostPaletteIndex == selectedPaletteIndex
                     && hoverGhostRotationKey == rotationKey;

        if (canReuse)
        {
            // 위치만 이동 — 자식(데칼 등)은 로컬 오프셋 유지되므로 자동으로 따라감
            Vector3 spawnPos = CalculateSpawnPosition(type, ghostPos, onSpawn, hoveredSpawnId);
            hoverGhost.transform.position = spawnPos;
            hoverGhostLocation = newLocation;
        }
        else
        {
            // 팔레트 변경 또는 회전 컨텍스트 변경 → 재생성
            DestroyHoverGhost();

            Vector3 spawnPos = CalculateSpawnPosition(type, ghostPos, onSpawn, hoveredSpawnId);
            hoverGhost = InstantiateMissilePrefab(palettePrefabs[selectedPaletteIndex], spawnPos,
                type, onSpawn, hoveredSpawnId);
            hoverGhostLocation     = newLocation;
            hoverGhostPaletteIndex = selectedPaletteIndex;
            hoverGhostRotationKey  = rotationKey;

            if (hoverGhost != null)
            {
                foreach (var col in hoverGhost.GetComponentsInChildren<Collider>(true))
                    col.enabled = false;
            }
        }
    }

    /// <summary>
    /// 회전/데칼 설정이 동일한지 판별하기 위한 키.
    /// 팔레트 인덱스 + 타일/스폰 여부 + 스폰 방향 접두사로 구성.
    /// </summary>
    private static string BuildRotationKey(int paletteIdx, bool onSpawn, string spawnId)
    {
        if (!onSpawn) return $"{paletteIdx}:tile";
        // 스폰: 방향 접두사(N:/S:/E:/W:/NE/NW/SE/SW)가 같으면 회전이 동일
        string dirPrefix = GetSpawnDirectionPrefix(spawnId);
        return $"{paletteIdx}:spawn:{dirPrefix}";
    }

    private static string GetSpawnDirectionPrefix(string spawnId)
    {
        if (spawnId == null) return "";
        // "N:3" → "N", "NE" → "NE"
        int colonIdx = spawnId.IndexOf(':');
        return colonIdx >= 0 ? spawnId.Substring(0, colonIdx) : spawnId;
    }

    private static void DestroyHoverGhost()
    {
        if (hoverGhost != null)
            Object.DestroyImmediate(hoverGhost);
        hoverGhost             = null;
        hoverGhostLocation     = null;
        hoverGhostPaletteIndex = -1;
        hoverGhostRotationKey  = null;
    }

    #endregion
}
