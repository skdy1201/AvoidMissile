using UnityEngine;

/// <summary>
/// 자식을 지정 횟수만큼 반복 실행
/// </summary>
[CreateAssetMenu(fileName = "RepeaterNode", menuName = "BehaviorTree/RepeaterNode")]
public class RepeaterNode : DecoratorNode
{
    #region Serialized Fields
    [SerializeField] private int repeatCount;
    #endregion

    #region Private/Protected Fields
    private int currentCount;
    #endregion

    #region Private/Protected Methods
    protected override NodeResult OnExecute(GameObject owner)
    {
        if (children.Count == 0)
        {
            Debug.LogError("No Children");
            return NodeResult.FAILURE;
        }

        NodeResult result = children[0].Execute(owner);

        if (result == NodeResult.RUNNING)
            return NodeResult.RUNNING;

        currentCount++;

        if (currentCount >= repeatCount)
        {
            currentCount = 0;
            return NodeResult.SUCCESS;
        }

        return NodeResult.RUNNING;
    }
    #endregion
}
