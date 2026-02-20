using UnityEngine;
using UnityEditor;
using GraphVisualizer;

/// <summary>
/// 행동트리의 노드를 시각화하기 위한 래퍼 클래스
/// </summary>
/// <remarks>
/// GraphVisualizer의 Node를 상속받아 구현
/// </remarks>
public class VisualBTNode : Node
{
    #region Private/Protected Fields
    private BehaviorNode targetNode;
    #endregion

    #region Public Methods
    /// <summary>
    /// BehaviorNode를 래핑하고 자식 노드도 재귀적으로 생성
    /// </summary>
    public VisualBTNode(BehaviorNode behaviorNode)
        : base(behaviorNode, 1f, behaviorNode != null && behaviorNode.WasExecutedThisFrame)
    {
        targetNode = behaviorNode;

        if (behaviorNode == null)
        {
            Debug.LogWarning("BehaviorNode empty!!");
            return;
        }

        foreach (var child in behaviorNode.Children)
        {
            VisualBTNode childNode = new VisualBTNode(child);
            AddChild(childNode);
        }
    }

    /// <summary>
    /// 노드 타입에 따른 색상 반환
    /// </summary>
    public override Color GetColor()
    {
        if (targetNode == null)
            return Color.gray;

        if (!targetNode.WasExecutedThisFrame && EditorApplication.isPlaying)
            return Color.gray;

        return targetNode.NodeType switch
        {
            BehaviorTreeNodeType.Composite => new Color(0.53f, 0.81f, 0.92f), // Sky Blue
            BehaviorTreeNodeType.Decorator => Color.yellow,
            BehaviorTreeNodeType.Leaf => Color.green,
            _ => Color.white
        };
    }

    /// <summary>
    /// Inspector에 표시할 노드 정보
    /// </summary>
    public override string ToString()
    {
        if (targetNode == null)
            return "Null Node";

        string status = targetNode.WasExecutedThisFrame
            ? targetNode.LastResult.ToString()
            : "Not Executed";

        return $"{targetNode.name}\nType: {targetNode.GetType().Name}\nStatus: {status}";
    }
    #endregion
}
