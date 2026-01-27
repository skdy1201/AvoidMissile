using UnityEngine;

/// <summary>
/// 자식 결과와 관계없이 항상 SUCCESS 반환
/// </summary>
[CreateAssetMenu(fileName = "SucceederNode", menuName = "BehaviorTree/SucceederNode")]
public class SucceederNode : DecoratorNode
{
    #region Private/Protected Methods
    protected override NodeResult OnExecute(GameObject owner)
    {
        if (children.Count == 0)
        {
            Debug.LogError("No Children");
            return NodeResult.FAILURE;
        }

        children[0].Execute(owner);
        return NodeResult.SUCCESS;
    }
    #endregion
}
