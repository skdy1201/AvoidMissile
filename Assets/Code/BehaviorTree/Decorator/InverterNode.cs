using UnityEngine;

/// <summary>
/// 자식 결과를 반전 (SUCCESS ↔ FAILURE)
/// </summary>
[CreateAssetMenu(fileName = "InverterNode", menuName = "BehaviorTree/InverterNode")]
public class InverterNode : DecoratorNode
{
    #region Private/Protected Methods
    protected override NodeResult OnExecute(GameObject owner)
    {
        if (children.Count == 0)
        {
            Debug.LogError("No Children");
            return NodeResult.FAILURE;
        }

        NodeResult result = children[0].Execute(owner);

        return result switch
        {
            NodeResult.SUCCESS => NodeResult.FAILURE,
            NodeResult.FAILURE => NodeResult.SUCCESS,
            _ => NodeResult.RUNNING
        };
    }
    #endregion
}
