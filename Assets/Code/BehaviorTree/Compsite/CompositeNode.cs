using UnityEngine;

/// <summary>
/// 여러 자식을 가질 수 있는 복합 노드 (Sequence, Selector 등)
/// </summary>
public abstract class CompositeNode : BehaviorNode
{

    #region Unity Lifecycle
    protected virtual void OnEnable()
    {
        NodeType = BehaviorTreeNodeType.Composite;
    }
    #endregion

    #region Public Methods
    public override void AddNode(BehaviorNode childNode)
    {
        children.Add(childNode);
    }
    #endregion
}
