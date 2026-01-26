using GraphVisualizer;
using System.Collections.Generic;

/// <summary>
/// 행동트리 시각화를 위한 그래프 컨테이너
/// </summary>
public class VisualBTGraph : Graph
{
    #region Private/Protected Fields
    private Node rootNode;
    #endregion

    #region Public Methods
    public VisualBTGraph(VisualBTNode node)
    {
        rootNode = node;
    }
    #endregion

    #region Private/Protected Methods
    protected override IEnumerable<Node> GetChildren(Node node)
    {
        return node.children;
    }

    protected override void Populate()
    {
        AddNodeHierarchy(rootNode);
    }
    #endregion
}
