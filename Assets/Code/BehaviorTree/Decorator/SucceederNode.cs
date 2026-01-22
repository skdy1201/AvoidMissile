using UnityEngine;

[CreateAssetMenu(fileName = "SucceederNode", menuName = "BehaviorTree/SucceederNode")]

public class SucceederNode : DecoratorNode
{
    public override NodeResult Execute(GameObject owner)
    {
        if (childrens.Count == 0)
        {
            Debug.LogError("No Children");
        }

        childrens[0].Execute(owner);

        return NodeResult.SUCCESS;
    }
}
