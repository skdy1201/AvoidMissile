using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 미사일 패턴 에디터 전용 씬뷰 툴바.
/// 에디터 모드 ↔ 기본 씬뷰 도구 전환 토글을 제공한다.
/// PatternEditorController.Open/Close에서 표시/숨김을 제어한다.
/// </summary>
[Overlay(typeof(SceneView), OverlayId, "Pattern Editor", defaultDisplay = false)]
[Icon("d_CustomTool")]
public class PatternEditorToolbar : ToolbarOverlay
{
    public const string OverlayId = "pattern-editor-toolbar";

    PatternEditorToolbar()
        : base(PatternEditorModeToggle.Id) { }

    #region Public API — 표시/숨김

    public static void Show()
    {
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) return;

        if (sceneView.TryGetOverlay(OverlayId, out var overlay))
        {
            overlay.displayed = true;
        }
    }

    public static void Hide()
    {
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) return;

        if (sceneView.TryGetOverlay(OverlayId, out var overlay))
        {
            overlay.displayed = false;
        }
    }

    #endregion
}

/// <summary>
/// 에디터 모드 토글 버튼.
/// ON: Tools.current = Tool.None → 좌클릭이 에디터 인터랙션으로.
/// OFF: 이전 Tool 복원 → 기본 씬뷰 도구 사용 가능.
/// </summary>
[EditorToolbarElement(Id, typeof(SceneView))]
public class PatternEditorModeToggle : EditorToolbarToggle
{
    public const string Id = "pattern-editor-mode-toggle";

    private static Tool savedTool = Tool.Move;
    private static bool editorModeActive;

    /// <summary>
    /// 외부에서 에디터 모드 상태를 확인할 수 있는 프로퍼티.
    /// </summary>
    public static bool EditorModeActive => editorModeActive;

    public PatternEditorModeToggle()
    {
        text = "Editor Mode";
        tooltip = "에디터 모드 ↔ 기본 씬뷰 도구 전환\n" +
                  "ON: 좌클릭으로 타일/미사일 선택·배치\n" +
                  "OFF: 기본 Move/Rotate/Scale 도구 사용";

        // 에디터 진입 시 기본 ON
        SetValueWithoutNotify(editorModeActive);
        this.RegisterValueChangedCallback(OnToggleChanged);
    }

    private void OnToggleChanged(ChangeEvent<bool> evt)
    {
        if (evt.newValue)
            ActivateEditorMode();
        else
            DeactivateEditorMode();
    }

    /// <summary>
    /// 에디터 모드 활성화 — 기본 도구 비활성, 좌클릭을 에디터가 사용.
    /// </summary>
    public static void ActivateEditorMode()
    {
        if (editorModeActive) return;

        savedTool = Tools.current;
        Tools.current = Tool.None;
        editorModeActive = true;

        PatternEditorSceneInteraction.SetInteractionEnabled(true);
    }

    /// <summary>
    /// 에디터 모드 비활성화 — 기본 도구 복원.
    /// </summary>
    public static void DeactivateEditorMode()
    {
        if (!editorModeActive) return;

        Tools.current = savedTool != Tool.None ? savedTool : Tool.Move;
        editorModeActive = false;

        PatternEditorSceneInteraction.SetInteractionEnabled(false);
    }

    /// <summary>
    /// 에디터 종료 시 상태 초기화.
    /// </summary>
    public static void Reset()
    {
        if (editorModeActive)
            DeactivateEditorMode();

        editorModeActive = false;
    }

}
