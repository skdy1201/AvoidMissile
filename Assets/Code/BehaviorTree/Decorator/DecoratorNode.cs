using UnityEngine;

/// <summary>
/// 단일 자식을 가지며 결과를 수정하는 데코레이터 노드
/// </summary>
public abstract class DecoratorNode : BehaviorNode
{
    #region Unity Lifecycle
    protected virtual void OnEnable()
    {
        NodeType = BehaviorTreeNodeType.Decorator;
    }

    protected virtual void OnValidate()
    {
        if (children.Count > 1)
        {
            Debug.LogWarning($"[{name}] Decorator는 자식 1개만 가능합니다. 초과분 제거됨.");
            children.RemoveRange(1, children.Count - 1);
        }
    }
    #endregion

    #region Public Methods
    public override void AddNode(BehaviorNode node)
    {
        if (children.Count == 0)
            children.Add(node);
        else
            Debug.LogWarning("Already Child Exist");
    }
    #endregion
}
