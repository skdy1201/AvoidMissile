using UnityEngine;
using UnityEditor;
using GraphVisualizer;

/// <summary>
/// 행동트리를 시각화하는 에디터 윈도우
/// </summary>
/// <remarks>
/// Selection 감지로 BehaviorTree를 자동 표시하고, Play Mode에서 실시간 추적
/// </remarks>
public class BehaviorTreeVisualizerWindow : EditorWindow
{
    #region Private/Protected Fields
    private IGraphRenderer renderer;
    private IGraphLayout layout;
    private GraphSettings graphSettings;
    private BehaviorTree currentTree;

    private static readonly float ToolbarHeight = 17f;
    private static readonly float DefaultMaximumNormalizedNodeSize = 0.8f;
    private static readonly float DefaultMaximumNodeSizeInPixels = 100.0f;
    private static readonly float DefaultAspectRatio = 1.5f;
    #endregion

    #region Public Methods
    [MenuItem("Window/Analysis/BehaviorTree Visualizer")]
    public static void ShowWindow()
    {
        GetWindow<BehaviorTreeVisualizerWindow>("BehaviorTree Visualizer");
    }
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        graphSettings.maximumNormalizedNodeSize = DefaultMaximumNormalizedNodeSize;
        graphSettings.maximumNodeSizeInPixels = DefaultMaximumNodeSizeInPixels;
        graphSettings.aspectRatio = DefaultAspectRatio;
        graphSettings.showInspector = true;
        graphSettings.showLegend = true;

        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void Update()
    {
        if (EditorApplication.isPlaying)
            Repaint();
    }

    private void OnInspectorUpdate()
    {
        if (!EditorApplication.isPlaying)
            Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (currentTree == null)
        {
            ShowMessage("Select a BehaviorTree asset or GameObject with BehaviorTreeRunner");
            return;
        }

        if (currentTree.RootNode == null)
        {
            ShowMessage("BehaviorTree has no root node");
            return;
        }

        var visualNode = new VisualBTNode(currentTree.RootNode);
        var graph = new VisualBTGraph(visualNode);
        graph.Refresh();

        if (graph.IsEmpty())
        {
            ShowMessage("BehaviorTree is empty");
            return;
        }

        if (layout == null)
            layout = new ReingoldTilford();

        layout.CalculateLayout(graph);

        var graphRect = new Rect(0, ToolbarHeight, position.width, position.height - ToolbarHeight);

        if (renderer == null)
            renderer = new VisualBTGraphRenderer();

        renderer.Draw(layout, graphRect, graphSettings);
    }
    #endregion

    #region Event Handlers
    private void OnSelectionChanged()
    {
        if (Selection.activeObject is BehaviorTree tree)
        {
            currentTree = tree;
            Repaint();
            return;
        }

        GameObject selectedGO = Selection.activeGameObject;
        if (selectedGO != null)
        {
            var runner = selectedGO.GetComponent<BehaviorTreeRunner>();
            if (runner != null && runner.TargetTree != null)
            {
                currentTree = runner.TargetTree;
                Repaint();
            }
        }
    }
    #endregion

    #region Private/Protected Methods
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Width(position.width));

        string treeName = currentTree != null ? currentTree.name : "None";
        EditorGUILayout.LabelField("Tree: " + treeName, GUILayout.Width(200));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
        {
            Repaint();
        }

        EditorGUILayout.EndHorizontal();
    }

    private static void ShowMessage(string msg)
    {
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUILayout.Label(msg);

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
    }
    #endregion
}