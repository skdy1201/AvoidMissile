using UnityEngine;

/// <summary>
/// 자식 노드를 순차적으로 실행. 하나라도 실패하면 실패
/// </summary>
[CreateAssetMenu(fileName = "SequenceNode", menuName = "BehaviorTree/SequenceNode")]
public class SequenceNode : CompositeNode
{
    #region Private Fields
    private int runningIndex = 0;
    #endregion

    #region Private/Protected Methods
    protected override NodeResult OnExecute(GameObject owner)
    {
        for(int i = runningIndex; i < children.Count; ++i)
        {
            NodeResult childResult = children[i].Execute(owner);

            if (childResult == NodeResult.FAILURE)
            {
                runningIndex = 0;
                return childResult;
            }
            else if(childResult == NodeResult.RUNNING)
            {
                runningIndex = i;
                return NodeResult.RUNNING;

            }
        }

        runningIndex = 0;
        return NodeResult.SUCCESS;
    }
    #endregion
}
