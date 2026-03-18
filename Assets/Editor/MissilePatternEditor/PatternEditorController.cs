using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 미사일 패턴 에디터 컨트롤러 — PlayScene을 Edit 모드로 열고 에디터 전용 Canvas를 생성합니다.
/// 씬 진입/종료, 게임 UI 비활성화, 에디터 Canvas 생명주기를 관리합니다.
/// 도메인 리로드 시 SessionState로 활성 상태를 복원하고 씬뷰 인터랙션을 재등록합니다.
/// </summary>
[InitializeOnLoad]
public static class PatternEditorController
{
    #region Domain Reload Recovery

    static PatternEditorController()
    {
        if (Active)
            EditorApplication.delayCall += OnDomainReload;
    }

    private static void OnDomainReload()
    {
        if (!Active) return;

        // 도메인 리로드 후 이벤트 재등록 + 씬뷰 인터랙션 복원
        EditorSceneManager.sceneClosing += OnSceneClosing;
        DeactivateGameUI();
        PatternEditorSceneInteraction.Initialize();

        Debug.Log("[PatternEditorController] 도메인 리로드 후 복원 완료.");
    }

    #endregion

    #region Constants

    private const string PlayScenePath = "Assets/Scene/Main/PlayScene.unity";
    private const string MenuPath      = "Tools/Missile Pattern Editor";

    #endregion

    #region Session Keys

    private const string SessionKeyActive         = "PatternEditorController_Active";
    private const string SessionKeySceneSetup     = "PatternEditorController_SceneSetup";

    #endregion

    #region Private Fields

    // 도메인 리로드(스크립트 재컴파일) 시 static 필드가 초기화되므로 SessionState로 보존
    private static bool Active
    {
        get => SessionState.GetBool(SessionKeyActive, false);
        set => SessionState.SetBool(SessionKeyActive, value);
    }

    // 멀티 씬 셋업을 "path,loaded,active;path,loaded,active;..." 형태로 직렬화
    private static string SceneSetup
    {
        get => SessionState.GetString(SessionKeySceneSetup, "");
        set => SessionState.SetString(SessionKeySceneSetup, value);
    }

    // 에디터 진입 시 비활성화한 기존 게임 오브젝트
    private static GameObject cachedCanvas;
    private static GameObject cachedPlayerCanvas;
    private static GameObject cachedEventSystem;
    private static GameObject cachedGreyVolume;

    // 에디터가 생성한 오브젝트
    private static GameObject editorCanvas;

    #endregion

    #region Public Methods

    [MenuItem(MenuPath)]
    public static void Toggle()
    {
        if (Active)
            Close();
        else
            Open();
    }

    public static void Open()
    {
        if (Active) return;

        // 현재 씬 저장 여부 확인
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;   // 사용자가 취소
        }

        // 멀티 씬 셋업 저장
        SaveSceneSetup();

        // PlayScene을 Edit 모드로 열기
        EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Single);

        // 기존 게임 UI 비활성화
        DeactivateGameUI();

        // 에디터 전용 Canvas 생성
        CreateEditorCanvas();

        // 씬뷰 인터랙션 초기화 (타일/스폰포인트 생성 + SceneView 콜백 등록)
        PatternEditorSceneInteraction.Initialize();

        Active = true;
        EditorSceneManager.sceneClosing += OnSceneClosing;

        Debug.Log("[PatternEditorController] 미사일 패턴 에디터를 시작합니다.");
    }

    public static void Close()
    {
        if (!Active) return;

        Cleanup();

        // 이전 멀티 씬 셋업 복원
        RestoreSceneSetup();

        Debug.Log("[PatternEditorController] 미사일 패턴 에디터를 종료합니다.");
    }

    #endregion

    #region Private Methods — Scene Setup

    private static void SaveSceneSetup()
    {
        int sceneCount = SceneManager.sceneCount;
        var parts      = new string[sceneCount];
        string activePath = EditorSceneManager.GetActiveScene().path;

        for (int i = 0; i < sceneCount; i++)
        {
            var scene    = SceneManager.GetSceneAt(i);
            bool loaded  = scene.isLoaded;
            bool active  = scene.path == activePath;
            parts[i]     = $"{scene.path},{loaded},{active}";
        }

        SceneSetup = string.Join(";", parts);
    }

    private static void RestoreSceneSetup()
    {
        string data = SceneSetup;
        if (string.IsNullOrEmpty(data)) return;

        string[] entries   = data.Split(';');
        string activePath  = "";
        bool firstLoaded   = true;

        for (int i = 0; i < entries.Length; i++)
        {
            string[] tokens = entries[i].Split(',');
            if (tokens.Length < 3) continue;

            string path  = tokens[0];
            bool loaded  = tokens[1] == "True";
            bool active  = tokens[2] == "True";

            if (string.IsNullOrEmpty(path)) continue;

            if (firstLoaded && loaded)
            {
                // 첫 번째 로드된 씬은 Single로 열어서 현재 PlayScene을 교체
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                firstLoaded = false;
            }
            else if (loaded)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            else
            {
                // 로드되지 않은 상태로 추가 (Hierarchy에 보이지만 미로드)
                EditorSceneManager.OpenScene(path, OpenSceneMode.AdditiveWithoutLoading);
            }

            if (active) activePath = path;
        }

        // 원래 활성 씬 복원
        if (!string.IsNullOrEmpty(activePath))
        {
            var scene = SceneManager.GetSceneByPath(activePath);
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.SetActiveScene(scene);
        }

        SceneSetup = "";
    }

    #endregion

    #region Private Methods

    private static void DeactivateGameUI()
    {
        cachedCanvas       = GameObject.Find("Canvas");
        cachedPlayerCanvas = GameObject.Find("PlayerCanvas");
        cachedEventSystem  = GameObject.Find("EventSystem");
        cachedGreyVolume   = GameObject.Find("GreyVolume");

        if (cachedCanvas != null)       cachedCanvas.SetActive(false);
        if (cachedPlayerCanvas != null)  cachedPlayerCanvas.SetActive(false);
        if (cachedEventSystem != null)   cachedEventSystem.SetActive(false);
        if (cachedGreyVolume != null)    cachedGreyVolume.SetActive(false);
    }

    private static void CreateEditorCanvas()
    {
        editorCanvas = new GameObject("EditorCanvas");

        var canvas         = editorCanvas.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        editorCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        editorCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    private static void Cleanup()
    {
        EditorSceneManager.sceneClosing -= OnSceneClosing;

        // 씬뷰 인터랙션 정리 (SceneView 콜백 해제 + 생성된 오브젝트 파괴)
        PatternEditorSceneInteraction.Cleanup();

        if (editorCanvas != null)
            Object.DestroyImmediate(editorCanvas);

        editorCanvas       = null;
        cachedCanvas       = null;
        cachedPlayerCanvas = null;
        cachedEventSystem  = null;
        cachedGreyVolume   = null;
        Active             = false;
    }

    #endregion

    #region Event Handlers

    // 사용자가 다른 씬을 열거나 에디터를 닫을 때 자동 정리
    private static void OnSceneClosing(Scene scene, bool removingScene)
    {
        if (!Active) return;

        var playScene = SceneManager.GetActiveScene();
        if (scene == playScene)
            Cleanup();
    }

    #endregion
}
