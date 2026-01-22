using UnityEngine;

[CreateAssetMenu(fileName = "SequenceNode", menuName = "BehaviorTree/SequenceNode")]

public class SequenceNode : CompositeNode
{

    public override NodeResult Execute(GameObject owner)
    {
        foreach(var child in childrens)
        {
            NodeResult childResult = child.Execute(owner);

            if(childResult == NodeResult.FAILURE)
                return childResult;
        }

        return NodeResult.SUCCESS;
    }
}
