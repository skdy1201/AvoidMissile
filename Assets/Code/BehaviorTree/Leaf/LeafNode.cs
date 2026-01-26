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
    #endregion

    #region Public Methods
    public override void AddNode(BehaviorNode childNode)
    {
        Debug.LogWarning("LeafNode cannot have children");
    }
    #endregion
}
