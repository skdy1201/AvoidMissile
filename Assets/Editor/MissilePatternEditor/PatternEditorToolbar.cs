using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 미사일 패턴 에디터 전용 씬뷰 툴바.
/// 에디터 모드 토글 + 시뮬레이션 재생 컨트롤을 제공한다.
/// PatternEditorController.Open/Close에서 표시/숨김을 제어한다.
/// </summary>
[Overlay(typeof(SceneView), OverlayId, "Pattern Editor", defaultDisplay = false)]
[Icon("d_CustomTool")]
public class PatternEditorToolbar : ToolbarOverlay
{
    public const string OverlayId = "pattern-editor-toolbar";

    PatternEditorToolbar()
        : base(
            PatternEditorModeToggle.Id,
            SimPrevButton.Id,
            SimPlayPauseButton.Id,
            SimNextButton.Id,
            SimSpeedButton.Id,
            SimLoopToggle.Id
        ) { }

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

    public static bool EditorModeActive => editorModeActive;

    public PatternEditorModeToggle()
    {
        text = "Editor Mode";
        tooltip = "에디터 모드 ↔ 기본 씬뷰 도구 전환\n" +
                  "ON: 좌클릭으로 타일/미사일 선택·배치\n" +
                  "OFF: 기본 Move/Rotate/Scale 도구 사용";

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

    public static void ActivateEditorMode()
    {
        if (editorModeActive) return;

        savedTool = Tools.current;
        Tools.current = Tool.None;
        editorModeActive = true;

        PatternEditorSceneInteraction.SetInteractionEnabled(true);
    }

    public static void DeactivateEditorMode()
    {
        if (!editorModeActive) return;

        Tools.current = savedTool != Tool.None ? savedTool : Tool.Move;
        editorModeActive = false;

        PatternEditorSceneInteraction.SetInteractionEnabled(false);
    }

    public static void Reset()
    {
        if (editorModeActive)
            DeactivateEditorMode();

        editorModeActive = false;
    }
}

#region Simulation Toolbar Elements

/// <summary>이전 구간/시작으로 점프.</summary>
[EditorToolbarElement(Id, typeof(SceneView))]
public class SimPrevButton : EditorToolbarButton
{
    public const string Id = "sim-prev-button";

    public SimPrevButton()
    {
        text    = "|<";
        tooltip = "이전 구간 시작점으로 점프 (구간 없으면 t=0)";
        clicked += PatternEditorSimulation.JumpToPrevSegmentOrStart;
    }
}

/// <summary>재생/일시정지 토글.</summary>
[EditorToolbarElement(Id, typeof(SceneView))]
public class SimPlayPauseButton : EditorToolbarButton
{
    public const string Id = "sim-play-pause-button";

    public SimPlayPauseButton()
    {
        text    = "\u25B6";  // ▶
        tooltip = "재생 / 일시정지 (Space)";
        clicked += OnClick;
        RegisterCallback<AttachToPanelEvent>(_ =>
            PatternEditorSimulation.OnStateChanged += UpdateVisual);
        RegisterCallback<DetachFromPanelEvent>(_ =>
            PatternEditorSimulation.OnStateChanged -= UpdateVisual);
    }

    private void OnClick()
    {
        PatternEditorSimulation.TogglePlay();
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        text = PatternEditorSimulation.IsPlaying ? "||" : "\u25B6";
    }
}

/// <summary>다음 구간/끝으로 점프.</summary>
[EditorToolbarElement(Id, typeof(SceneView))]
public class SimNextButton : EditorToolbarButton
{
    public const string Id = "sim-next-button";

    public SimNextButton()
    {
        text    = ">|";
        tooltip = "다음 구간 시작점으로 점프 (구간 없으면 끝)";
        clicked += PatternEditorSimulation.JumpToNextSegmentOrEnd;
    }
}

/// <summary>배속 순환 버튼 (0.25x → 0.5x → 1x → 2x → Custom).</summary>
[EditorToolbarElement(Id, typeof(SceneView))]
public class SimSpeedButton : EditorToolbarButton
{
    public const string Id = "sim-speed-button";

    public SimSpeedButton()
    {
        text    = "1x";
        tooltip = "배속 순환 (0.25x → 0.5x → 1x → 2x → Custom)";
        clicked += OnClick;
        RegisterCallback<AttachToPanelEvent>(_ =>
            PatternEditorSimulation.OnStateChanged += UpdateVisual);
        RegisterCallback<DetachFromPanelEvent>(_ =>
            PatternEditorSimulation.OnStateChanged -= UpdateVisual);
    }

    private void OnClick()
    {
        PatternEditorSimulation.CycleSpeed();
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        text = PatternEditorSimulation.CurrentSpeedLabel;
    }
}

/// <summary>루프 토글.</summary>
[EditorToolbarElement(Id, typeof(SceneView))]
public class SimLoopToggle : EditorToolbarToggle
{
    public const string Id = "sim-loop-toggle";

    public SimLoopToggle()
    {
        text    = "Loop";
        tooltip = "끝 도달 시 처음부터 재개";
        SetValueWithoutNotify(PatternEditorSimulation.LoopPlayback);
        this.RegisterValueChangedCallback(OnToggleChanged);
        RegisterCallback<AttachToPanelEvent>(_ =>
            PatternEditorSimulation.OnStateChanged += UpdateVisual);
        RegisterCallback<DetachFromPanelEvent>(_ =>
            PatternEditorSimulation.OnStateChanged -= UpdateVisual);
    }

    private void OnToggleChanged(ChangeEvent<bool> evt)
    {
        PatternEditorSimulation.LoopPlayback = evt.newValue;
    }

    private void UpdateVisual()
    {
        SetValueWithoutNotify(PatternEditorSimulation.LoopPlayback);
    }
}

#endregion
