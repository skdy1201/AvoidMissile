using UnityEngine;

/// <summary>
/// 자식 노드 중 하나라도 성공하면 성공
/// </summary>
[CreateAssetMenu(fileName = "SelectNode", menuName = "BehaviorTree/SelectNode")]
public class SelectNode : CompositeNode
{
    #region Private Fields
    private int runningIndex = 0;
    #endregion

    #region Private/Protected Methods
    protected override NodeResult OnExecute(GameObject owner)
    {
        for (int i = runningIndex; i < children.Count; ++i)
        {
            NodeResult result = children[i].Execute(owner);

            if (result == NodeResult.SUCCESS)
            {
                runningIndex = 0;
                return result;
            }
            else if (result == NodeResult.RUNNING)
            {
                runningIndex = i;
                return NodeResult.RUNNING;
            }
        }

        runningIndex = 0;
        return NodeResult.FAILURE;
    }
    #endregion
}
