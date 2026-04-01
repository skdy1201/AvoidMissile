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
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        if (Active)
        {
            // 도메인 리로드 후, 초기화
            EditorApplication.delayCall += OnDomainReload;                
        }
    }

    private static void OnDomainReload()
    {
        if (!Active) return;

        // 도메인 리로드 후 이벤트 재등록 + 씬뷰 인터랙션 복원
        EditorSceneManager.sceneClosing += OnSceneClosing;
        DeactivateGameUI();
        PatternEditorSceneInteraction.Initialize();
        PatternEditorSimulation.Initialize();

        // 툴바 + 에디터 모드 복원
        PatternEditorToolbar.Show();
        PatternEditorModeToggle.ActivateEditorMode();

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

    /// <summary>
    /// 에디터 세션 값을 저장
    /// 도메인 리로드(스크립트 재컴파일) 시 static 필드가 초기화되므로 SessionState로 보존
    /// </summary>
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
    private static GameObject savedCanvas;
    private static GameObject savedPlayerCanvas;
    private static GameObject savedEventSystem;
    private static GameObject savedGreyVolume;

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

    /// <summary>
    /// PlayScene을 그대로 열고, 일부 비활성화 및 추가 UI 활성화로 Editor 환경 세팅
    /// </summary>
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

        // 시뮬레이션 초기화 (dt 루프 등록)
        PatternEditorSimulation.Initialize();

        // 툴바 표시 + 에디터 모드 활성화
        PatternEditorToolbar.Show();
        PatternEditorModeToggle.ActivateEditorMode();

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

    /// <summary>
    /// 씬 설정 저장 함수
    /// </summary>
    /// <remarks>
    /// SessionState는 문자열 하나만 저장 가능하니, 연결해야 한다.
    /// </remarks>
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

    /// <summary>
    /// SessionState로 저장해놨던 기존 씬 정보를 복원
    /// </summary>
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

    /// <summary>
    /// 필요없는 UI들을 비활성화 시키기
    /// </summary>
    private static void DeactivateGameUI()
    {
        savedCanvas       = GameObject.Find("Canvas");
        savedPlayerCanvas = GameObject.Find("PlayerCanvas");
        savedEventSystem  = GameObject.Find("EventSystem");
        savedGreyVolume   = GameObject.Find("GreyVolume");

        if (savedCanvas != null)       savedCanvas.SetActive(false);
        if (savedPlayerCanvas != null)  savedPlayerCanvas.SetActive(false);
        if (savedEventSystem != null)   savedEventSystem.SetActive(false);
        if (savedGreyVolume != null)    savedGreyVolume.SetActive(false);
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

        // 툴바 숨김 + 에디터 모드 해제
        PatternEditorModeToggle.Reset();
        PatternEditorToolbar.Hide();

        // 시뮬레이션 정리 (dt 루프 해제 + 상태 초기화)
        PatternEditorSimulation.Cleanup();

        // 씬뷰 인터랙션 정리 (SceneView 콜백 해제 + 생성된 오브젝트 파괴)
        PatternEditorSceneInteraction.Cleanup();

        if (editorCanvas != null)
            Object.DestroyImmediate(editorCanvas);

        editorCanvas       = null;
        savedCanvas        = null;
        savedPlayerCanvas  = null;
        savedEventSystem   = null;
        savedGreyVolume    = null;
        Active             = false;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// 패턴 에디터 활성 상태에서 Play 모드 진입 차단
    /// </summary>
    /// <param name="state"> 에디터 상태 열거형 </param>
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode || !Active) return;

        // Play 모드 차단
        EditorApplication.isPlaying = false;
        EditorUtility.DisplayDialog(
            "미사일 패턴 에디터",
            "패턴 에디터가 활성화된 상태에서는 Play 모드를 사용할 수 없습니다.\n먼저 Tools > Missile Pattern Editor로 에디터를 종료해주세요.",
            "확인");
    }

    /// <summary>
    /// 임의로 사용자가 다른 씬을 열거나 에디터를 닫을 때 자동 정리
    /// </summary>
    /// <remarks>
    /// 정상 종료(Close)를 거치지 않고 씬이 닫힐 때의 안전망.
    /// Active 플래그·이벤트 핸들러 등 에디터 상태를 정리하여 꼬임을 방지한다.
    /// </remarks>
    /// <param name="scene"> 닫히려는 씬 </param>
    /// <param name="removingScene"> 씬이 완전히 제거되는지 여부 </param>
    private static void OnSceneClosing(Scene scene, bool removingScene)
    {
        if (!Active) return;

        var playScene = SceneManager.GetActiveScene();
        if (scene == playScene)
            Cleanup();
    }

    #endregion
}
