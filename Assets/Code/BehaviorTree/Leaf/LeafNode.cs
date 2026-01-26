using UnityEngine;

/// <summary>
/// 실제 행동을 수행하는 말단 노드
/// </summary>
public abstract class LeafNode : BehaviorNode
{
    #region Unity Lifecycle
    protected virtual void OnEnable()
    {
        NodeType = BehaviorTreeNodeType.Leaf;
    }

    protected virtual void OnValidate()
    {
        if (children.Count > 0)
        {
            Debug.LogWarning($"[{name}] LeafNode는 자식을 가질 수 없습니다. 모두 제거됨.");
            children.Clear();
        }
    }
    #endregion

    #region Public Methods
    public override void AddNode(BehaviorNode childNode)
    {
        Debug.LogWarning("LeafNode cannot have children");
    }
    #endregion
}
