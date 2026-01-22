using System;
using UnityEngine;

[CreateAssetMenu(fileName = "InverterNode", menuName = "BehaviorTree/InverterNode")]

public class InverterNode : DecoratorNode
{
    public override NodeResult Execute(GameObject owner)
    {
        if (childrens.Count  == 0)
        {
            Debug.LogError("No Children");
        }

        NodeResult result = childrens[0].Execute(owner);

        switch (result)
        {
            case NodeResult.SUCCESS:
                return NodeResult.FAILURE;
            case NodeResult.FAILURE:
                return NodeResult.SUCCESS;
            default:
                return NodeResult.RUNNING;
        }
    }
}
