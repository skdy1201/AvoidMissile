using UnityEngine;

[CreateAssetMenu(fileName = "RepeaterNode", menuName = "BehaviorTree/RepeaterNode")]
public class RepeaterNode : DecoratorNode
{
    private int repeatCount;
    private int currentCount = 0;

    public RepeaterNode(int count) { repeatCount = count; }

    public override NodeResult Execute(GameObject owner)
    {
        if (childrens.Count == 0)
        {
            Debug.LogError("No Children");
        }

        NodeResult result = childrens[0].Execute(owner);
        
        if(result == NodeResult.RUNNING)
        {
            return NodeResult.RUNNING;
        }

        ++currentCount;

        if(currentCount >= repeatCount)
        {
            currentCount = 0;
            return NodeResult.SUCCESS;
        }

        return NodeResult.RUNNING;
    }
}
